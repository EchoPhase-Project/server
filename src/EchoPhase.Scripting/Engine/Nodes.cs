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
    }

    internal sealed class Literal(object? value) : Node
    {
        public object? Value { get; } = value;

        public override object? Evaluate(IDictionary<string, object> variables) => Value;
    }

    internal enum UnaryOp { Negate, Not }

    internal sealed class Unary(UnaryOp op, Node operand) : Node
    {
        public override object? Evaluate(IDictionary<string, object> variables)
        {
            var value = operand.Evaluate(variables);
            return op == UnaryOp.Negate ? -Values.ToNumber(value) : !Values.ToBool(value);
        }
    }

    internal enum BinaryOp { Add, Subtract, Multiply, Divide, Modulo, Equal, NotEqual, Less, Greater, LessEq, GreaterEq, And, Or }

    internal sealed class Binary(BinaryOp op, Node left, Node right) : Node
    {
        public override object? Evaluate(IDictionary<string, object> variables)
        {
            // Short-circuit: the right side runs only when it decides the result.
            if (op == BinaryOp.And)
                return Values.ToBool(left.Evaluate(variables)) && Values.ToBool(right.Evaluate(variables));
            if (op == BinaryOp.Or)
                return Values.ToBool(left.Evaluate(variables)) || Values.ToBool(right.Evaluate(variables));

            var l = left.Evaluate(variables);
            var r = right.Evaluate(variables);
            return op switch
            {
                BinaryOp.Add => Add(l, r),
                BinaryOp.Subtract => Values.ToNumber(l) - Values.ToNumber(r),
                BinaryOp.Multiply => Values.ToNumber(l) * Values.ToNumber(r),
                BinaryOp.Divide => Values.ToNumber(l) / NonZero(r),
                BinaryOp.Modulo => Values.ToNumber(l) % NonZero(r),
                BinaryOp.Equal => Values.AreEqual(l, r),
                BinaryOp.NotEqual => !Values.AreEqual(l, r),
                BinaryOp.Less => Values.Compare(l, r) < 0,
                BinaryOp.Greater => Values.Compare(l, r) > 0,
                BinaryOp.LessEq => Values.Compare(l, r) <= 0,
                BinaryOp.GreaterEq => Values.Compare(l, r) >= 0,
                _ => throw new NotSupportedException(op.ToString())
            };
        }

        // Numbers (numeric strings included, as before) add; otherwise text joins.
        private static object Add(object? l, object? r)
        {
            if (Values.TryNumber(l, out var ln) && Values.TryNumber(r, out var rn))
                return ln + rn;
            if (Values.Unwrap(l) is string || Values.Unwrap(r) is string)
                return Values.Format(l) + Values.Format(r);
            throw new InvalidOperationException($"Cannot add {Values.Describe(l)} and {Values.Describe(r)}.");
        }

        private static double NonZero(object? value)
        {
            var number = Values.ToNumber(value);
            return number == 0 ? throw new DivideByZeroException("Division by zero.") : number;
        }
    }

    internal sealed class Conditional(Node condition, Node whenTrue, Node whenFalse) : Node
    {
        public override object? Evaluate(IDictionary<string, object> variables)
            => Values.ToBool(condition.Evaluate(variables))
                ? whenTrue.Evaluate(variables)
                : whenFalse.Evaluate(variables);
    }

    internal sealed class Call(string name, Func<object?[], object?> function, Node[] arguments) : Node
    {
        public string Name { get; } = name;

        public override object? Evaluate(IDictionary<string, object> variables)
        {
            var values = new object?[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
                values[i] = arguments[i].Evaluate(variables);
            return function(values);
        }
    }

    /// <summary>One step of a path: <c>.name</c> or <c>[expression]</c>.</summary>
    internal abstract record PathSegment;

    internal sealed record MemberSegment(string Name) : PathSegment;

    internal sealed record IndexSegment(Node Index) : PathSegment;

    /// <summary>
    /// <c>root.member[index]…</c>. An unknown root variable is an error (a
    /// typo should not pass silently); past the root, a missing member or a
    /// null is null all the way, like optional chaining.
    /// </summary>
    internal sealed class PathNode(string root, IReadOnlyList<PathSegment> segments) : Node
    {
        private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> Properties = new();

        public string Root { get; } = root;

        public IReadOnlyList<PathSegment> Segments { get; } = segments;

        public override object? Evaluate(IDictionary<string, object> variables)
        {
            if (!variables.TryGetValue(Root, out var current))
                throw new KeyNotFoundException($"Variable '{Root}' not found.");

            foreach (var segment in Segments)
            {
                if (current is null)
                    return null;
                current = segment switch
                {
                    MemberSegment m => Member(current, m.Name),
                    IndexSegment ix => Index(current, ix.Index.Evaluate(variables)),
                    _ => throw new NotSupportedException()
                };
            }

            return current;
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
