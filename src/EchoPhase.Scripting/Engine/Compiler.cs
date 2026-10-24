// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using System.Reflection;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// Turns a hot expression into a .NET delegate (System.Linq.Expressions),
    /// so evaluating it no longer walks the tree node by node. It emits calls
    /// to the very helpers the interpreter uses (comparisons, lookups,
    /// functions), so both give the same values, errors and budget use; what
    /// it does not compile (lambdas, comprehensions, slices, literals of
    /// lists) it hands back to the interpreter. See <see cref="TieredNode"/>
    /// for when.
    /// </summary>
    internal static class Compiler
    {
        private static readonly ParameterExpression Variables = Expression.Parameter(typeof(IDictionary<string, object>), "variables");

        private static readonly MethodInfo EvaluateMethod = typeof(Node).GetMethod(nameof(Node.Evaluate))
            ?? throw new MissingMethodException(nameof(Node), nameof(Node.Evaluate));
        private static readonly MethodInfo ToBoolMethod = Helper(typeof(Values), nameof(Values.ToBool));
        private static readonly MethodInfo ToNumberMethod = Helper(typeof(Values), nameof(Values.ToNumber));
        private static readonly MethodInfo BoxBoolMethod = Helper(typeof(Boxes), nameof(Boxes.Bool));
        private static readonly MethodInfo ApplyMethod = Helper(typeof(Binary), nameof(Binary.Apply));
        private static readonly MethodInfo NonZeroMethod = Helper(typeof(Binary), nameof(Binary.NonZero));
        private static readonly MethodInfo CompareDoublesMethod = Helper(typeof(Binary), nameof(Binary.CompareDoubles));
        private static readonly MethodInfo CompareNumberWithMethod = Helper(typeof(Binary), nameof(Binary.CompareNumberWith));
        private static readonly MethodInfo CompareWithNumberMethod = Helper(typeof(Binary), nameof(Binary.CompareWithNumber));
        private static readonly MethodInfo LookupMethod = Helper(typeof(PathNode), nameof(PathNode.Lookup));
        private static readonly MethodInfo MemberMethod = Helper(typeof(PathNode), nameof(PathNode.Member));
        private static readonly MethodInfo IndexMethod = Helper(typeof(PathNode), nameof(PathNode.Index));
        private static readonly MethodInfo RentMethod = Helper(typeof(ArgumentArrays), nameof(ArgumentArrays.Rent));
        private static readonly MethodInfo ReturnMethod = Helper(typeof(ArgumentArrays), nameof(ArgumentArrays.Return));

        public static Func<IDictionary<string, object>, object?> CompileValue(Node node)
            => Expression.Lambda<Func<IDictionary<string, object>, object?>>(Value(node), Variables).Compile();

        public static Func<IDictionary<string, object>, double> CompileNumber(Node node)
            => Expression.Lambda<Func<IDictionary<string, object>, double>>(Number(node), Variables).Compile();

        // The node's value, as object.
        private static Expression Value(Node node) => node switch
        {
            TieredNode tiered => Value(tiered.Inner),
            Literal literal => Expression.Constant(literal.Value, typeof(object)),
            Unary { Op: UnaryOp.Negate } => Box(Number(node)),
            Unary { Op: UnaryOp.Not } not => BoxBool(Expression.Not(ToBool(Value(not.Operand)))),
            Binary { IsNumeric: true } => Box(Number(node)),
            Binary { NumericComparison: true } binary => BoxBool(Comparison(binary)),
            Binary { Op: BinaryOp.And } and => BoxBool(Expression.AndAlso(ToBool(Value(and.Left)), ToBool(Value(and.Right)))),
            Binary { Op: BinaryOp.Or } or => BoxBool(Expression.OrElse(ToBool(Value(or.Left)), ToBool(Value(or.Right)))),
            Binary binary => Expression.Call(ApplyMethod, Expression.Constant(binary.Op), Value(binary.Left), Value(binary.Right)),
            Conditional c => Expression.Condition(ToBool(Value(c.Condition)), Value(c.WhenTrue), Value(c.WhenFalse), typeof(object)),
            PathNode path when Plain(path.Segments) => Steps(Expression.Call(LookupMethod, Variables, Expression.Constant(path.Root)), path.Segments),
            Access access when Plain(access.Segments) => Steps(Value(access.Target), access.Segments),
            Call call => Invoke(call),
            _ => Interpret(node)
        };

        // The node's value as a number: unboxed where the interpreter is (EvaluateNumber), else ToNumber of the value.
        private static Expression Number(Node node) => node switch
        {
            TieredNode tiered => Number(tiered.Inner),
            Literal { Value: double d } => Expression.Constant(d),
            Unary { Op: UnaryOp.Negate } negate => Expression.Negate(Number(negate.Operand)),
            Binary { IsNumeric: true } b => b.Op switch
            {
                BinaryOp.Add => Expression.Add(Number(b.Left), Number(b.Right)),
                BinaryOp.Subtract => Expression.Subtract(Number(b.Left), Number(b.Right)),
                BinaryOp.Multiply => Expression.Multiply(Number(b.Left), Number(b.Right)),
                BinaryOp.Divide => Expression.Divide(Number(b.Left), Expression.Call(NonZeroMethod, Number(b.Right))),
                _ => Expression.Modulo(Number(b.Left), Expression.Call(NonZeroMethod, Number(b.Right))),
            },
            Conditional c => Expression.Condition(ToBool(Value(c.Condition)), Number(c.WhenTrue), Number(c.WhenFalse)),
            _ => Expression.Call(ToNumberMethod, Value(node))
        };

        // A comparison with a number on a side, operands in source order.
        private static Expression Comparison(Binary b)
        {
            var op = Expression.Constant(b.Op);
            if (b.Left.IsNumeric && b.Right.IsNumeric)
                return Expression.Call(CompareDoublesMethod, op, Number(b.Left), Number(b.Right));
            return b.Left.IsNumeric
                ? Expression.Call(CompareNumberWithMethod, op, Number(b.Left), Value(b.Right))
                : Expression.Call(CompareWithNumberMethod, op, Value(b.Left), Number(b.Right));
        }

        private static bool Plain(PathSegment[] segments) => segments.All(s => s is MemberSegment or IndexSegment);

        // .name and [index] steps; a null stops the path (null all the way, and
        // later index expressions are not evaluated), as in the interpreter.
        private static Expression Steps(Expression start, PathSegment[] segments)
        {
            var current = start;
            foreach (var segment in segments)
            {
                var value = Expression.Variable(typeof(object), "step");
                var next = segment switch
                {
                    MemberSegment m => Expression.Call(MemberMethod, value, Expression.Constant(m.Name)),
                    IndexSegment ix => Expression.Call(IndexMethod, value, Value(ix.Index)),
                    _ => throw new NotSupportedException()
                };
                current = Expression.Block(typeof(object), [value],
                    Expression.Assign(value, current),
                    Expression.Condition(Expression.Equal(value, Expression.Constant(null)),
                        Expression.Constant(null, typeof(object)), next, typeof(object)));
            }
            return current;
        }

        // The function with its arguments in a pooled array, returned after the call, as Call.Evaluate does.
        private static Expression Invoke(Call call)
        {
            var array = Expression.Variable(typeof(object?[]), "arguments");
            var body = new List<Expression>();
            for (int i = 0; i < call.Arguments.Length; i++)
                body.Add(Expression.Assign(Expression.ArrayAccess(array, Expression.Constant(i)), Value(call.Arguments[i])));
            body.Add(Expression.Invoke(Expression.Constant(call.Function), array, Variables));

            return Expression.Block(typeof(object), [array],
                Expression.Assign(array, Expression.Call(RentMethod, Expression.Constant(call.Arguments.Length))),
                Expression.TryFinally(Expression.Block(typeof(object), body), Expression.Call(ReturnMethod, array)));
        }

        private static Expression Interpret(Node node)
            => Expression.Call(Expression.Constant(node, typeof(Node)), EvaluateMethod, Variables);

        private static Expression ToBool(Expression value) => Expression.Call(ToBoolMethod, value);

        private static Expression BoxBool(Expression condition) => Expression.Call(BoxBoolMethod, condition);

        private static Expression Box(Expression number) => Expression.Convert(number, typeof(object));

        private static MethodInfo Helper(Type type, string name)
            => type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
               ?? throw new MissingMethodException(type.Name, name);
    }

    /// <summary>
    /// Interpreted first, compiled once hot. Most expressions run a few times
    /// and are not worth the ~1 ms a compile costs; one run in a loop or a
    /// lambda body over a large list is, so after <see cref="Threshold"/>
    /// evaluations one thread compiles it while the others keep interpreting.
    /// A failed compile leaves it interpreted. The node is shared by threads.
    /// </summary>
    internal sealed class TieredNode(Node inner) : Node
    {
        public const int Threshold = 100;

        private int _calls;
        private int _numberCalls;
        private int _compiling;
        private int _compilingNumber;
        private Func<IDictionary<string, object>, object?>? _value;
        private Func<IDictionary<string, object>, double>? _number;

        public Node Inner => inner;

        /// <summary>Tiered, unless there is nothing to compile: a literal, or a bare variable.</summary>
        public static Node Wrap(Node node)
            => node is Literal or TieredNode || (node is PathNode { Segments.Length: 0 }) ? node : new TieredNode(node);

        public override bool IsNumeric => inner.IsNumeric;

        public override bool IsConstant => inner.IsConstant;

        public override object? Evaluate(IDictionary<string, object> variables)
        {
            var compiled = Volatile.Read(ref _value);
            if (compiled is not null)
                return compiled(variables);
            if (++_calls >= Threshold && Interlocked.Exchange(ref _compiling, 1) == 0)
                Volatile.Write(ref _value, Try(() => Compiler.CompileValue(inner)));
            return inner.Evaluate(variables);
        }

        public override double EvaluateNumber(IDictionary<string, object> variables)
        {
            var compiled = Volatile.Read(ref _number);
            if (compiled is not null)
                return compiled(variables);
            if (++_numberCalls >= Threshold && Interlocked.Exchange(ref _compilingNumber, 1) == 0)
                Volatile.Write(ref _number, Try(() => Compiler.CompileNumber(inner)));
            return inner.EvaluateNumber(variables);
        }

        // A compile that fails (an expression the compiler mishandles) keeps the interpreter: never worse than before.
        private static T? Try<T>(Func<T> compile) where T : class
        {
            try
            {
                return compile();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return null;
            }
        }
    }
}
