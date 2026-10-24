// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// A parsed expression. Immutable, so one parse serves every evaluation
    /// on every thread; only the variables differ.
    /// </summary>
    internal abstract class Node
    {
        public abstract object? Evaluate(IDictionary<string, object> variables);

        /// <summary>
        /// Known when parsed to give a number (number literals and arithmetic
        /// on them): <see cref="EvaluateNumber"/> then never boxes, and an
        /// expression like x * 2 + 1 boxes only its result, if anything.
        /// </summary>
        public virtual bool IsNumeric => false;

        /// <summary>
        /// Its value depends on nothing but its own text: no variables, no
        /// impure functions. Such a node can be computed when parsed.
        /// </summary>
        public virtual bool IsConstant => false;

        /// <summary>The value as a number, without boxing where the node knows how.</summary>
        public virtual double EvaluateNumber(IDictionary<string, object> variables) => Values.ToNumber(Evaluate(variables));
    }

    internal sealed class Literal(object? value) : Node
    {
        private readonly double _number = value is double d ? d : 0;

        public object? Value { get; } = value;

        public override object? Evaluate(IDictionary<string, object> variables) => Value;

        public override bool IsConstant => true;

        public override bool IsNumeric => Value is double;

        public override double EvaluateNumber(IDictionary<string, object> variables)
            => Value is double ? _number : Values.ToNumber(Value);
    }

    internal enum UnaryOp { Negate, Not }

    internal sealed class Unary(UnaryOp op, Node operand) : Node
    {
        public UnaryOp Op => op;

        public Node Operand => operand;

        public override bool IsNumeric => op == UnaryOp.Negate;

        public override bool IsConstant => operand.IsConstant;

        public override object? Evaluate(IDictionary<string, object> variables)
            => op == UnaryOp.Negate
                ? -operand.EvaluateNumber(variables)
                : Boxes.Bool(!Values.ToBool(operand.Evaluate(variables)));

        public override double EvaluateNumber(IDictionary<string, object> variables)
            => op == UnaryOp.Negate ? -operand.EvaluateNumber(variables) : base.EvaluateNumber(variables);
    }

    internal enum BinaryOp { Add, Subtract, Multiply, Divide, Modulo, Equal, NotEqual, Less, Greater, LessEq, GreaterEq, And, Or, In, NotIn }

    internal sealed class Binary(BinaryOp op, Node left, Node right) : Node
    {
        // Arithmetic always gives a number; + only when both sides do (else it may join text).
        private readonly bool _numeric = op is BinaryOp.Subtract or BinaryOp.Multiply or BinaryOp.Divide or BinaryOp.Modulo
                                         || (op == BinaryOp.Add && left.IsNumeric && right.IsNumeric);

        // A comparison with a number on either side compares doubles, unboxed.

        public BinaryOp Op => op;

        public Node Left => left;

        public Node Right => right;

        public bool NumericComparison { get; } = op is >= BinaryOp.Equal and <= BinaryOp.GreaterEq
                                                   && (left.IsNumeric || right.IsNumeric);

        public override bool IsNumeric => _numeric;

        public override bool IsConstant => left.IsConstant && right.IsConstant;

        public override double EvaluateNumber(IDictionary<string, object> variables)
        {
            if (!_numeric)
                return base.EvaluateNumber(variables);

            double l = left.EvaluateNumber(variables);
            return op switch
            {
                BinaryOp.Add => l + right.EvaluateNumber(variables),
                BinaryOp.Subtract => l - right.EvaluateNumber(variables),
                BinaryOp.Multiply => l * right.EvaluateNumber(variables),
                BinaryOp.Divide => l / NonZero(right.EvaluateNumber(variables)),
                _ => l % NonZero(right.EvaluateNumber(variables)),
            };
        }

        public override object? Evaluate(IDictionary<string, object> variables)
        {
            if (_numeric)
                return EvaluateNumber(variables);
            if (NumericComparison)
                return Boxes.Bool(CompareNumbers(variables));

            // Short-circuit: the right side runs only when it decides the result.
            if (op == BinaryOp.And)
                return Boxes.Bool(Values.ToBool(left.Evaluate(variables)) && Values.ToBool(right.Evaluate(variables)));
            if (op == BinaryOp.Or)
                return Boxes.Bool(Values.ToBool(left.Evaluate(variables)) || Values.ToBool(right.Evaluate(variables)));

            var l = left.Evaluate(variables);
            var r = right.Evaluate(variables);
            return Apply(op, l, r);
        }

        /// <summary>Any operator but the short-circuit ones, on two evaluated values: the general rules.</summary>
        internal static object Apply(BinaryOp op, object? l, object? r)
        {
            return op switch
            {
                BinaryOp.Add => Add(l, r),
                // Shared boxes: a comparison in filter or count allocates nothing.
                BinaryOp.Equal => Boxes.Bool(Values.AreEqual(l, r)),
                BinaryOp.NotEqual => Boxes.Bool(!Values.AreEqual(l, r)),
                BinaryOp.Less => Boxes.Bool(Values.Compare(l, r) < 0),
                BinaryOp.Greater => Boxes.Bool(Values.Compare(l, r) > 0),
                BinaryOp.LessEq => Boxes.Bool(Values.Compare(l, r) <= 0),
                BinaryOp.GreaterEq => Boxes.Bool(Values.Compare(l, r) >= 0),
                BinaryOp.In => Boxes.Bool(Values.Contains(r, l)),
                BinaryOp.NotIn => Boxes.Bool(!Values.Contains(r, l)),
                _ => throw new NotSupportedException(op.ToString())
            };
        }

        // Operands in source order: left, then right.
        private bool CompareNumbers(IDictionary<string, object> variables)
        {
            if (left.IsNumeric && right.IsNumeric)
                return CompareDoubles(op, left.EvaluateNumber(variables), right.EvaluateNumber(variables));
            if (left.IsNumeric)
                return CompareNumberWith(op, left.EvaluateNumber(variables), right.Evaluate(variables));
            return CompareWithNumber(op, left.Evaluate(variables), right.EvaluateNumber(variables));
        }

        internal static bool CompareDoubles(BinaryOp op, double l, double r) => op switch
        {
            BinaryOp.Equal => l == r,
            BinaryOp.NotEqual => l != r,
            BinaryOp.Less => l.CompareTo(r) < 0,
            BinaryOp.Greater => l.CompareTo(r) > 0,
            BinaryOp.LessEq => l.CompareTo(r) <= 0,
            _ => l.CompareTo(r) >= 0,
        };

        // The same answers as Values.AreEqual and Values.Compare give for a number
        // against anything: == is false for null or non-numeric values; ordering
        // them is an error.

        /// <summary>number OP other.</summary>
        internal static bool CompareNumberWith(BinaryOp op, double number, object? other)
            => Values.TryNumber(other, out var otherNumber)
                ? CompareDoubles(op, number, otherNumber)
                : Unordered(op, $"Cannot order {Values.Describe(number)} and {Values.Describe(other)}.");

        /// <summary>other OP number.</summary>
        internal static bool CompareWithNumber(BinaryOp op, object? other, double number)
            => Values.TryNumber(other, out var otherNumber)
                ? CompareDoubles(op, otherNumber, number)
                : Unordered(op, $"Cannot order {Values.Describe(other)} and {Values.Describe(number)}.");

        private static bool Unordered(BinaryOp op, string message)
            => op is BinaryOp.Equal or BinaryOp.NotEqual
                ? op == BinaryOp.NotEqual
                : throw new InvalidOperationException(message);

        // Numbers (numeric strings included, as before) add; otherwise text joins.
        private static object Add(object? l, object? r)
        {
            if (Values.TryNumber(l, out var ln) && Values.TryNumber(r, out var rn))
                return ln + rn;
            if (Values.Unwrap(l) is string || Values.Unwrap(r) is string)
                return Values.Format(l) + Values.Format(r);
            throw new InvalidOperationException($"Cannot add {Values.Describe(l)} and {Values.Describe(r)}.");
        }

        internal static double NonZero(double number)
            => number == 0 ? throw new DivideByZeroException("Division by zero.") : number;
    }

    internal sealed class Conditional(Node condition, Node whenTrue, Node whenFalse) : Node
    {
        public Node Condition => condition;

        public Node WhenTrue => whenTrue;

        public Node WhenFalse => whenFalse;

        public override bool IsNumeric => whenTrue.IsNumeric && whenFalse.IsNumeric;

        public override bool IsConstant => condition.IsConstant && whenTrue.IsConstant && whenFalse.IsConstant;

        public override object? Evaluate(IDictionary<string, object> variables)
            => Values.ToBool(condition.Evaluate(variables))
                ? whenTrue.Evaluate(variables)
                : whenFalse.Evaluate(variables);

        public override double EvaluateNumber(IDictionary<string, object> variables)
            => Values.ToBool(condition.Evaluate(variables))
                ? whenTrue.EvaluateNumber(variables)
                : whenFalse.EvaluateNumber(variables);
    }

    internal sealed class Call(string name, Func<object?[], IDictionary<string, object>, object?> function, bool pure, Node[] arguments) : Node
    {
        public string Name { get; } = name;

        public Func<object?[], IDictionary<string, object>, object?> Function => function;

        public Node[] Arguments => arguments;

        public override bool IsConstant => pure && arguments.All(a => a.IsConstant);

        // Argument arrays come from a per-thread pool and go back after the
        // call: functions read their arguments during the call only, and every
        // result they return is built anew, never a view of the array.
        public override object? Evaluate(IDictionary<string, object> variables)
        {
            var values = ArgumentArrays.Rent(arguments.Length);
            try
            {
                for (int i = 0; i < arguments.Length; i++)
                    values[i] = arguments[i].Evaluate(variables);
                return function(values, variables);
            }
            finally
            {
                ArgumentArrays.Return(values);
            }
        }
    }

    /// <summary><c>[a, b, c]</c>: a new list on every evaluation (all-constant ones are folded when parsed).</summary>
    internal sealed class ListLiteral(Node[] items) : Node
    {
        public override object? Evaluate(IDictionary<string, object> variables)
        {
            Budget.Spend(items.Length);
            var list = new List<object?>(items.Length);
            foreach (var item in items)
                list.Add(item.Evaluate(variables));
            return list;
        }
    }

    /// <summary><c>{ name: value, 'any key': value }</c>: a new dictionary on every evaluation.</summary>
    internal sealed class ObjectLiteral((string Key, Node Value)[] members) : Node
    {
        public override object? Evaluate(IDictionary<string, object> variables)
        {
            Budget.Spend(members.Length);
            var dict = new Dictionary<string, object?>(members.Length);
            foreach (var (key, value) in members)
                dict[key] = value.Evaluate(variables);
            return dict;
        }
    }

    /// <summary>One <c>for names in source</c> or <c>if condition</c> of a comprehension.</summary>
    internal abstract record ComprehensionClause;

    internal sealed record ForClause(string[] Names, Node Source) : ComprehensionClause;

    internal sealed record IfClause(Node Condition) : ComprehensionClause;

    /// <summary>
    /// Python's <c>[expr for x in xs if cond]</c>: clauses nest left to right
    /// (<c>for a in xs for b in ys</c> is every pair). A list is walked by
    /// item, a text by character, an object by key; <c>for k, v in items(d)</c>
    /// unpacks. Names bound here shadow variables, as lambda parameters do.
    /// </summary>
    /// <param name="reusable">No lambda inside: each clause's scope can be rebound per item, not made anew.</param>
    internal sealed class Comprehension(Node element, ComprehensionClause[] clauses, bool reusable) : Node
    {
        public override object? Evaluate(IDictionary<string, object> variables)
        {
            var result = new List<object?>();
            // Per evaluation (the node is shared between threads), one slot per clause.
            Walk(0, variables, result, reusable ? new BoundScope?[clauses.Length] : null);
            return result;
        }

        private void Walk(int clause, IDictionary<string, object> scope, List<object?> result, BoundScope?[]? frames)
        {
            if (clause == clauses.Length)
            {
                Budget.Spend(1);
                result.Add(element.Evaluate(scope));
                return;
            }

            switch (clauses[clause])
            {
                case IfClause filter:
                    if (Values.ToBool(filter.Condition.Evaluate(scope)))
                        Walk(clause + 1, scope, result, frames);
                    break;
                case ForClause each:
                    var source = each.Source.Evaluate(scope);
                    if (Values.Unwrap(source) is IList list)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            Budget.Spend(1);
                            Walk(clause + 1, Bind(each.Names, list[i], scope, frames, clause), result, frames);
                        }
                        break;
                    }
                    foreach (var item in Iterate(source))
                    {
                        Budget.Spend(1);
                        Walk(clause + 1, Bind(each.Names, item, scope, frames, clause), result, frames);
                    }
                    break;
            }
        }

        internal static IEnumerable<object?> Iterate(object? source) => Values.Unwrap(source) switch
        {
            null => [],
            string text => text.Select(c => (object?)c.ToString()),
            IDictionary<string, object?> dict => dict.Keys.Select(k => (object?)k).ToList(),
            IList list => list.Cast<object?>(),
            var v => throw new InvalidOperationException($"Cannot iterate over {Values.Describe(v)}.")
        };

        private static IDictionary<string, object> Bind(
            string[] names, object? item, IDictionary<string, object> outer, BoundScope?[]? frames, int clause)
        {
            if (names.Length == 1)
            {
                if (frames?[clause] is Scope1 one)
                {
                    (one.Value, one.Outer) = (item, outer);
                    return one;
                }
                var scope = new Scope1(names[0], item, outer);
                frames?[clause] = scope;
                return scope;
            }

            if (Values.Unwrap(item) is not IList parts || parts.Count != names.Length)
                throw new InvalidOperationException(
                    $"Cannot unpack {Values.Describe(item)} into {names.Length} names ({string.Join(", ", names)}).");

            if (names.Length == 2)
            {
                if (frames?[clause] is Scope2 two)
                {
                    (two.Value1, two.Value2, two.Outer) = (parts[0], parts[1], outer);
                    return two;
                }
                var scope = new Scope2(names[0], parts[0], names[1], parts[1], outer);
                frames?[clause] = scope;
                return scope;
            }

            var values = new object?[names.Length];
            parts.CopyTo(values, 0);
            return new ScopeN(names, values, outer);
        }
    }

    /// <summary><c>x =&gt; x.age &gt; 18</c>, <c>(acc, x) =&gt; acc + x</c>: a function value for filter, map and the like.</summary>
    /// <param name="reusable">No lambda inside its body: nothing can keep its scope past a call.</param>
    internal sealed class Lambda(string[] parameters, Node body, bool reusable) : Node
    {
        public string[] Parameters { get; } = parameters;

        public Node Body { get; } = body;

        public bool Reusable { get; } = reusable;

        public override object? Evaluate(IDictionary<string, object> variables) => new ScriptFunction(this, variables);
    }

    /// <summary>
    /// A lambda with the variables it was written among. Parameters shadow
    /// them; missing arguments are null, extra ones are ignored, so a
    /// one-parameter lambda also serves where an index is passed too.
    /// </summary>
    public sealed class ScriptFunction
    {
        // The lambda's own node: its parameters, body and whether a scope can be reused.
        // One reference instead of three keeps a function value small: many are made, once per outer call.
        private readonly Lambda _lambda;
        private readonly IDictionary<string, object> _variables;

        // One scope rebound on every call when the body holds no lambda (none
        // can capture it); a call while one is running (recursion through a
        // variable) gets a fresh one. A function value belongs to the
        // evaluation that made it, on its thread.
        private bool _busy;
        private BoundScope? _cached; // a Scope1 or a Scope2, by arity

        internal ScriptFunction(Lambda lambda, IDictionary<string, object> variables)
        {
            _lambda = lambda;
            _variables = variables;
        }

        private string[] Parameters => _lambda.Parameters;

        private Node Body => _lambda.Body;

        private bool Reusable => _lambda.Reusable;

        /// <summary>
        /// Lambdas running inside one another on this thread. Syntax nests them
        /// only so far, but a lambda stored in a variable can be passed to map
        /// from its own body; this turns that endless recursion into an error
        /// before it overflows the stack (which would end the process).
        /// </summary>
        public const int MaxDepth = 128;

        [ThreadStatic]
        private static int _depth;

        public int Arity => Parameters.Length;

        /// <summary>The body is arithmetic: <see cref="InvokeNumber"/> gives its value without boxing.</summary>
        internal bool ReturnsNumber => Body.IsNumeric;

        // One small scope object per call, no dictionary; the index is boxed
        // only for a lambda that takes it (map(xs, x => ...) never does).

        public object? Invoke(object? item)
        {
            Budget.Spend(1);
            return Parameters.Length switch
            {
                0 => Run(_variables),
                1 => With1(item),
                _ => Invoke([item])
            };
        }

        /// <summary>An item and its position: what filter, map and the like pass.</summary>
        public object? Invoke(object? item, int index)
        {
            Budget.Spend(1);
            return Parameters.Length switch
            {
                0 => Run(_variables),
                1 => With1(item),
                2 => With2(item, Boxes.Index(index)),
                _ => Invoke([item, Boxes.Index(index)])
            };
        }

        /// <summary>Like <see cref="Invoke(object?, int)"/>, for a number: unboxed when the body is arithmetic.</summary>
        public double InvokeNumber(object? item, int index)
        {
            Budget.Spend(1);
            return Parameters.Length switch
            {
                0 => RunNumber(_variables),
                1 => With1Number(item),
                2 => With2Number(item, Boxes.Index(index)),
                _ => Values.ToNumber(Invoke([item, Boxes.Index(index)]))
            };
        }

        public object? Invoke(object? first, object? second, int index)
        {
            Budget.Spend(1);
            return Parameters.Length switch
            {
                0 => Run(_variables),
                1 => With1(first),
                2 => With2(first, second),
                _ => Invoke([first, second, Boxes.Index(index)])
            };
        }

        public object? Invoke(params object?[] arguments)
        {
            Budget.Spend(1);
            if (Parameters.Length == 0)
                return Run(_variables);

            var values = new object?[Parameters.Length];
            Array.Copy(arguments, values, Math.Min(arguments.Length, values.Length));
            return Run(new ScopeN(Parameters, values, _variables));
        }

        private object? Run(IDictionary<string, object> scope)
        {
            Enter();
            try
            {
                return Body.Evaluate(scope);
            }
            finally
            {
                _depth--;
            }
        }

        private double RunNumber(IDictionary<string, object> scope)
        {
            Enter();
            try
            {
                return Body.EvaluateNumber(scope);
            }
            finally
            {
                _depth--;
            }
        }

        private static void Enter()
        {
            if (++_depth > MaxDepth)
            {
                _depth--;
                throw new InvalidOperationException($"Lambdas call each other more than {MaxDepth} deep (a lambda passed to itself?).");
            }
        }

        private object? With1(object? a)
        {
            var scope = Bound1(a, out var reused);
            try
            {
                return Run(scope);
            }
            finally
            {
                if (reused)
                    Free(scope);
            }
        }

        private double With1Number(object? a)
        {
            var scope = Bound1(a, out var reused);
            try
            {
                return RunNumber(scope);
            }
            finally
            {
                if (reused)
                    Free(scope);
            }
        }

        private object? With2(object? a, object? b)
        {
            var scope = Bound2(a, b, out var reused);
            try
            {
                return Run(scope);
            }
            finally
            {
                if (reused)
                    Free(scope);
            }
        }

        private double With2Number(object? a, object? b)
        {
            var scope = Bound2(a, b, out var reused);
            try
            {
                return RunNumber(scope);
            }
            finally
            {
                if (reused)
                    Free(scope);
            }
        }

        private BoundScope Bound1(object? a, out bool reused)
        {
            reused = Reusable && !_busy;
            if (!reused)
                return new Scope1(Parameters[0], a, _variables);
            var one = (Scope1)(_cached ??= new Scope1(Parameters[0], null, _variables));
            one.Value = a;
            _busy = true;
            return one;
        }

        private BoundScope Bound2(object? a, object? b, out bool reused)
        {
            reused = Reusable && !_busy;
            if (!reused)
                return new Scope2(Parameters[0], a, Parameters[1], b, _variables);
            var two = (Scope2)(_cached ??= new Scope2(Parameters[0], null, Parameters[1], null, _variables));
            (two.Value1, two.Value2) = (a, b);
            _busy = true;
            return two;
        }

        // Drops the items it held (they may be large) and frees it for the next call.
        private void Free(BoundScope scope)
        {
            switch (scope)
            {
                case Scope1 one:
                    one.Value = null;
                    break;
                case Scope2 two:
                    (two.Value1, two.Value2) = (null, null);
                    break;
            }
            _busy = false;
        }

        public override string ToString() => $"({string.Join(", ", Parameters)}) => …";
    }

    /// <summary>Steps on a computed value: <c>find(xs, f).name</c>; null all the way, like a path.</summary>
    internal sealed class Access(Node target, PathSegment[] segments) : Node
    {
        public Node Target => target;

        public PathSegment[] Segments => segments;

        public override bool IsConstant => target.IsConstant && segments.All(s => s switch
        {
            MemberSegment => true,
            IndexSegment ix => ix.Index.IsConstant,
            SliceSegment sl => sl.Start?.IsConstant != false && sl.End?.IsConstant != false && sl.Step?.IsConstant != false,
            _ => false
        });

        public override object? Evaluate(IDictionary<string, object> variables)
        {
            var current = target.Evaluate(variables);
            // for over an array: foreach over the interface would allocate an enumerator per read.
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (current is null)
                    return null;
                current = PathNode.Step(current, segment, variables);
            }
            return current;
        }
    }

    /// <summary>One step of a path: <c>.name</c> or <c>[expression]</c>.</summary>
    internal abstract record PathSegment;

    internal sealed record MemberSegment(string Name) : PathSegment;

    internal sealed record IndexSegment(Node Index) : PathSegment;

    /// <summary><c>[start:end:step]</c>, each part optional.</summary>
    internal sealed record SliceSegment(Node? Start, Node? End, Node? Step) : PathSegment;

    /// <summary>
    /// <c>root.member[index]…</c>. An unknown root variable is an error (a
    /// typo should not pass silently); past the root, a missing member or a
    /// null is null all the way, like optional chaining.
    /// </summary>
    internal sealed class PathNode(string root, PathSegment[] segments) : Node
    {
        private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> Properties = new();

        public string Root { get; } = root;

        public PathSegment[] Segments { get; } = segments;

        public override object? Evaluate(IDictionary<string, object> variables)
        {
            var current = Lookup(variables, Root);

            var path = Segments;
            for (int i = 0; i < path.Length; i++)
            {
                var segment = path[i];
                if (current is null)
                    return null;
                current = Step(current, segment, variables);
            }

            return current;
        }

        /// <summary>A variable by name; an unknown one is an error (a typo should not pass silently).</summary>
        internal static object? Lookup(IDictionary<string, object> variables, string name)
            => BoundScope.Find(variables, name, out var value) ? value : throw new KeyNotFoundException($"Variable '{name}' not found.");

        internal static object? Step(object current, PathSegment segment, IDictionary<string, object> variables) => segment switch
        {
            MemberSegment m => Member(current, m.Name),
            IndexSegment ix => Index(current, ix.Index.Evaluate(variables)),
            SliceSegment s => Slice(current, s.Start?.Evaluate(variables), s.End?.Evaluate(variables), s.Step?.Evaluate(variables)),
            _ => throw new NotSupportedException()
        };

        /// <summary>
        /// Python's slice of a list or text: <c>[1:3]</c>, <c>[-2:]</c>,
        /// <c>[::-1]</c>. Bounds are clamped, never out of range; null bounds
        /// are the defaults.
        /// </summary>
        internal static object? Slice(object current, object? start, object? end, object? step)
        {
            int by = Values.Unwrap(step) is null ? 1 : WholeNumber(step, "step");
            if (by == 0)
                throw new InvalidOperationException("A slice step cannot be zero.");

            switch (Values.Unwrap(current))
            {
                case IList list:
                    var items = new List<object?>();
                    foreach (var i in SliceIndices(list.Count, start, end, by))
                    {
                        Budget.Spend(1);
                        items.Add(list[i]);
                    }
                    return items;
                case string text:
                    var chars = new System.Text.StringBuilder();
                    foreach (var i in SliceIndices(text.Length, start, end, by))
                        chars.Append(text[i]);
                    Budget.Spend(chars.Length);
                    return chars.ToString();
                case null:
                    return null;
                default:
                    throw new InvalidOperationException($"{Values.Describe(current)} cannot be sliced.");
            }
        }

        // CPython's PySlice_AdjustIndices, then the walk.
        private static IEnumerable<int> SliceIndices(int length, object? start, object? end, int step)
        {
            int Adjust(object? bound, int whenNull, string what)
            {
                if (Values.Unwrap(bound) is null)
                    return whenNull;
                var i = WholeNumber(bound, what);
                if (i < 0)
                    return Math.Max(i + length, step < 0 ? -1 : 0);
                return i >= length ? (step < 0 ? length - 1 : length) : i;
            }

            int from = Adjust(start, step < 0 ? length - 1 : 0, "start");
            int to = Adjust(end, step < 0 ? -1 : length, "end");
            for (int i = from; step > 0 ? i < to : i > to; i += step)
                yield return i;
        }

        private static int WholeNumber(object? value, string what)
        {
            var number = Values.ToNumber(value);
            if (number != Math.Floor(number))
                throw new InvalidOperationException($"A slice {what} must be a whole number, not {Values.Format(number)}.");
            return (int)Math.Clamp(number, int.MinValue + 1, int.MaxValue);
        }

        internal static object? Member(object current, string name) => current switch
        {
            IDictionary<string, object?> dict => dict.TryGetValue(name, out var v) ? v : null,
            IReadOnlyDictionary<string, object?> dict => dict.TryGetValue(name, out var v) ? v : null,
            JsonElement { ValueKind: JsonValueKind.Object } json => json.TryGetProperty(name, out var p) ? Values.FromJson(p) : null,
            JsonElement => null,
            string => null,
            _ => Properties.GetOrAdd((current.GetType(), name), static k => k.Item1.GetProperty(k.Item2))?.GetValue(current)
        };

        internal static object? Index(object current, object? index)
        {
            index = Values.Unwrap(index);
            switch (Values.Unwrap(current))
            {
                case IList list:
                    var i = ListIndex(index, list.Count);
                    return i >= 0 && i < list.Count ? list[i] : null;
                case string s:
                    var c = ListIndex(index, s.Length);
                    return c >= 0 && c < s.Length ? s[c].ToString() : null;
                case IDictionary<string, object?> dict:
                    return dict.TryGetValue(Values.Format(index), out var v) ? v : null;
                default:
                    throw new InvalidOperationException($"{Values.Describe(current)} cannot be indexed.");
            }
        }

        /// <summary>A whole-number index; negative counts from the end (-1 is the last).</summary>
        internal static int ListIndex(object? index, int count)
        {
            var number = Values.ToNumber(index);
            if (number != Math.Floor(number))
                throw new InvalidOperationException($"Index {Values.Format(number)} is not a whole number.");
            var i = (int)number;
            return i < 0 ? count + i : i;
        }
    }
}
