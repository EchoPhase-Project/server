// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;

namespace EchoPhase.Scripting.Engine
{
    // Binary search over a list sorted by sort() (the same order: nulls, then
    // NaN, then numbers by value, or text ordinally), and toSet() for
    // repeated "x in set" checks in O(1). Like Python's bisect, the list is
    // not checked for order: that would cost the O(n) the search avoids, and
    // an unsorted list gives an unspecified answer. The value decides the
    // order: a number compares keys as numbers (numeric text too, as sort
    // does), text compares them as text. Keys (the optional lambda) are
    // computed only for the ~log2(n) probed elements.
    internal static partial class Functions
    {
        private static readonly Dictionary<string, Definition> Search = new(StringComparer.OrdinalIgnoreCase)
        {
            // bisectLeft(xs, v, fn?): the first position whose key is not below v (C++'s lower_bound).
            ["bisectLeft"] = new(2, 3, a => (double)Bound(a, "bisectLeft", right: false)),
            // bisectRight(xs, v, fn?): the position after the last key equal to v (upper_bound).
            ["bisectRight"] = new(2, 3, a => (double)Bound(a, "bisectRight", right: true)),
            // binarySearch(xs, v, fn?): the index of the first key equal to v, or -1.
            ["binarySearch"] = new(2, 3, a => (double)FirstEqual(a, "binarySearch")),
            // toSet(xs, fn?): { key: true } for each value (as text, like ==), so "x in set" is O(1).
            ["toSet"] = new(1, 2, a =>
            {
                var (xs, fn) = (ListOf(a, "toSet"), OptionalFn(a, 1, "toSet"));
                var set = new Dictionary<string, object?>(xs.Count, StringComparer.Ordinal);
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    set[Values.Format(fn is null ? xs[i] : fn.Invoke(xs[i], i))] = Boxes.True;
                }
                return set;
            }),
        };

        /// <summary>The value searched for, typed once: what every probed key is compared with.</summary>
        private readonly record struct SearchValue(bool IsNull, bool IsText, double Number, string? Text, object? Original);

        private static int Bound(object?[] arguments, string name, bool right)
        {
            var (list, fn, value) = Probe(arguments, name);
            return fn is null
                ? LowerBound(new PlainKeys(list), list.Count, value, right)
                : LowerBound(new LambdaKeys(list, fn), list.Count, value, right);
        }

        private static int FirstEqual(object?[] arguments, string name)
        {
            var (list, fn, value) = Probe(arguments, name);
            return fn is null
                ? FirstEqual(new PlainKeys(list), list.Count, value)
                : FirstEqual(new LambdaKeys(list, fn), list.Count, value);
        }

        private static int FirstEqual<TKeys>(TKeys keys, int count, SearchValue value) where TKeys : struct, IKeys
        {
            int i = LowerBound(keys, count, value, right: false);
            return i < count && CompareKey(keys.At(i), value) == 0 ? i : -1;
        }

        private static (IList List, ScriptFunction? Fn, SearchValue Value) Probe(object?[] arguments, string name)
        {
            var list = Values.Unwrap(arguments[0]) switch
            {
                null => Array.Empty<object?>(),
                IList l => l,
                var v => throw new InvalidOperationException($"'{name}' needs a sorted list, not {Values.Describe(v)}.")
            };

            var raw = Values.Unwrap(arguments[1]);
            var value = raw switch
            {
                null => new SearchValue(true, false, 0, null, null),
                string text => new SearchValue(false, true, 0, text, raw),
                _ when Values.TryNumber(raw, out var number) => new SearchValue(false, false, number, null, raw),
                _ => throw new InvalidOperationException($"'{name}' searches for a number or a text, not {Values.Describe(raw)}.")
            };

            return (list, arguments.Length > 2 ? Fn(arguments, 2, name) : null, value);
        }

        // How a probe gets its key. Structs, so the search loop is compiled
        // once per kind with the access inlined: no closure or delegate per call.
        private interface IKeys
        {
            object? At(int index);
        }

        private readonly struct PlainKeys(IList list) : IKeys
        {
            public object? At(int index)
            {
                Budget.Spend(1);
                return Values.Unwrap(list[index]);
            }
        }

        private readonly struct LambdaKeys(IList list, ScriptFunction fn) : IKeys
        {
            public object? At(int index) => Values.Unwrap(fn.Invoke(list[index], index));
        }

        // The first position whose key is >= value (> value when right): the
        // classic halving loop; count shrinks to 0 in ~log2(n) probes.
        private static int LowerBound<TKeys>(TKeys keys, int count, SearchValue value, bool right) where TKeys : struct, IKeys
        {
            int low = 0;
            while (count > 0)
            {
                int half = count >> 1;
                int c = CompareKey(keys.At(low + half), value);
                if (c < 0 || (right && c == 0))
                {
                    low += half + 1;
                    count -= half + 1;
                }
                else
                {
                    count = half;
                }
            }
            return low;
        }

        // key - value as -1, 0, 1, in sort's order: null < NaN < numbers (-0 == 0); text ordinally.
        private static int CompareKey(object? key, SearchValue value)
        {
            if (key is null)
                return value.IsNull ? 0 : -1;
            if (value.IsNull)
                return 1;

            if (value.IsText)
                return key is string text
                    ? Math.Sign(string.CompareOrdinal(text, value.Text))
                    : throw new InvalidOperationException(
                        $"Cannot compare {Values.Describe(key)} with the text {Values.Describe(value.Original)}: search a list of texts with a text.");

            if (!Values.TryNumber(key, out var number))
                throw new InvalidOperationException(
                    $"Cannot compare {Values.Describe(key)} with the number {Values.Describe(value.Original)}: search a list of numbers with a number.");

            bool keyNaN = double.IsNaN(number), valueNaN = double.IsNaN(value.Number);
            if (keyNaN || valueNaN)
                return keyNaN == valueNaN ? 0 : keyNaN ? -1 : 1;
            return number.CompareTo(value.Number) switch { < 0 => -1, > 0 => 1, _ => 0 };
        }
    }
}
