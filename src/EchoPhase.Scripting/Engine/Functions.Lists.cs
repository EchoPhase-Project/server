// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;

namespace EchoPhase.Scripting.Engine
{
    // Lists and objects. A lambda gets the item and its index:
    // filter(users, u => u.active), map(items, (x, i) => i + ': ' + x.name).
    // Nothing is changed in place: every function returns a new list.
    internal static partial class Functions
    {
        private static readonly Dictionary<string, Definition> Lists = new(StringComparer.OrdinalIgnoreCase)
        {
            // The hot ones are plain loops over the list: no iterator objects per call.
            ["filter"] = new(2, 2, a =>
            {
                var (xs, fn) = (ListOf(a, "filter"), Fn(a, 1, "filter"));
                var result = new List<object?>();
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    var x = xs[i];
                    if (Values.ToBool(fn.Invoke(x, i)))
                        result.Add(x);
                }
                return result;
            }),
            ["map"] = new(2, 2, a =>
            {
                var (xs, fn) = (ListOf(a, "map"), Fn(a, 1, "map"));
                var result = new List<object?>(xs.Count);
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    result.Add(fn.Invoke(xs[i], i));
                }
                return result;
            }),
            ["find"] = new(2, 2, a => IndexWhere(a, "find", out var found) >= 0 ? found : null),
            ["findIndex"] = new(2, 2, a => (double)IndexWhere(a, "findIndex", out _)),
            ["some"] = new(1, 2, a => Boxes.Bool(Truths(a, "some", stopAt: true, whenStopped: true, otherwise: false))),
            ["every"] = new(1, 2, a => Boxes.Bool(Truths(a, "every", stopAt: false, whenStopped: false, otherwise: true))),
            ["count"] = new(1, 2, a =>
            {
                var xs = ListOf(a, "count");
                if (a.Length < 2)
                {
                    Budget.Spend(xs.Count);
                    return (double)xs.Count;
                }
                var fn = Fn(a, 1, "count");
                int count = 0;
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    if (Values.ToBool(fn.Invoke(xs[i], i)))
                        count++;
                }
                return (double)count;
            }),
            ["sum"] = new(1, 2, a => SumOf(a, "sum", out _)),
            // null for an empty list.
            ["avg"] = new(1, 2, a => SumOf(a, "avg", out var count) is var sum && count > 0 ? sum / count : null),
            // Ascending by the item (or by what the lambda picks); stable.
            ["sort"] = new(1, 2, a => Sorted(a, "sort", descending: false)),
            ["sortDesc"] = new(1, 2, a => Sorted(a, "sortDesc", descending: true)),
            // The first of each value (or of each key the lambda picks); 1 and '1' are the same, as with ==.
            ["unique"] = new(1, 2, a =>
            {
                var (xs, fn) = (ListOf(a, "unique"), OptionalFn(a, 1, "unique"));
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var result = new List<object?>();
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    if (seen.Add(Values.Format(fn is null ? xs[i] : fn.Invoke(xs[i], i))))
                        result.Add(xs[i]);
                }
                return result;
            }),
            // { key: [items] } by the lambda's value as text, keys in order of first appearance.
            ["groupBy"] = new(2, 2, a =>
            {
                var (xs, fn) = (ListOf(a, "groupBy"), Fn(a, 1, "groupBy"));
                var groups = new Dictionary<string, List<object?>>(StringComparer.Ordinal);
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    var key = Values.Format(fn.Invoke(xs[i], i));
                    if (groups.TryGetValue(key, out var group))
                        group.Add(xs[i]);
                    else
                        groups[key] = [xs[i]];
                }
                // Keys stay in order of first appearance.
                return groups.ToDictionary(g => g.Key, g => (object?)g.Value, StringComparer.Ordinal);
            }),
            // reduce(items, (sum, x) => sum + x.price, 0)
            ["reduce"] = new(3, 3, a =>
            {
                var (xs, fn) = (ListOf(a, "reduce"), Fn(a, 1, "reduce"));
                var acc = a[2];
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    acc = fn.Invoke(acc, xs[i], i);
                }
                return acc;
            }),
            ["first"] = new(1, 2, a => Edge(a, "first", fromEnd: false)),
            ["last"] = new(1, 2, a => Edge(a, "last", fromEnd: true)),
            // slice(x, start, end?, step?) = x[start:end:step], for lists and text; null bounds are the defaults.
            ["slice"] = new(1, 4, a => Values.Unwrap(a[0]) is { } x
                ? PathNode.Slice(x, a.ElementAtOrDefault(1), a.ElementAtOrDefault(2), a.ElementAtOrDefault(3))
                : null),
            // splice(list, start, deleteCount?, ...items): JavaScript's, on a copy; returns the new list.
            ["splice"] = new(2, Many, a => Splice(a)),
            // enumerate(['a', 'b']) -> [[0, 'a'], [1, 'b']]; enumerate(xs, 1) counts from 1.
            ["enumerate"] = new(1, 2, a =>
            {
                var xs = ListOf(a, "enumerate");
                double from = a.Length > 1 ? Values.ToNumber(a[1]) : 0;
                var result = new List<object?>(xs.Count);
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    result.Add(new List<object?> { from + i, xs[i] });
                }
                return result;
            }),
            // zip([1, 2], ['a', 'b']) -> [[1, 'a'], [2, 'b']]; as long as the shortest.
            ["zip"] = new(1, Many, a =>
            {
                var lists = new IList[a.Length];
                int length = int.MaxValue;
                for (int l = 0; l < a.Length; l++)
                {
                    lists[l] = ListOf([a[l]], "zip");
                    length = Math.Min(length, lists[l].Count);
                }
                var result = new List<object?>(length);
                for (int i = 0; i < length; i++)
                {
                    Budget.Spend(1);
                    var row = new List<object?>(lists.Length);
                    foreach (var list in lists)
                        row.Add(list[i]);
                    result.Add(row);
                }
                return result;
            }),
            // items({ a: 1 }) -> [['a', 1]], for: [k + '=' + v for k, v in items(o)]
            ["items"] = new(1, 1, a => Pairs(a[0], "items", (key, value) => new List<object?> { key, value })),
            ["take"] = new(2, 2, a => Range(ListOf(a, "take"), 0, Count(a[1]))),
            ["skip"] = new(2, 2, a =>
            {
                var xs = ListOf(a, "skip");
                return Range(xs, Count(a[1]), xs.Count);
            }),
            ["reverse"] = new(1, 1, a =>
            {
                if (Values.Unwrap(a[0]) is string s)
                {
                    var chars = s.ToCharArray();
                    Array.Reverse(chars);
                    return new string(chars);
                }
                var xs = ListOf(a, "reverse");
                Budget.Spend(xs.Count);
                var result = new List<object?>(xs.Count);
                for (int i = xs.Count - 1; i >= 0; i--)
                    result.Add(xs[i]);
                return result;
            }),
            // One level: [[1, 2], 3] -> [1, 2, 3]
            ["flatten"] = new(1, 1, a =>
            {
                var xs = ListOf(a, "flatten");
                var result = new List<object?>(xs.Count);
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    if (Values.Unwrap(xs[i]) is IList inner)
                    {
                        Budget.Spend(inner.Count);
                        for (int j = 0; j < inner.Count; j++)
                            result.Add(inner[j]);
                    }
                    else
                    {
                        result.Add(xs[i]);
                    }
                }
                return result;
            }),
            // range(3) -> [0, 1, 2]; range(1, 4) -> [1, 2, 3]; range(0, 10, 5) -> [0, 5]
            ["range"] = new(1, 3, a => Range(a)),
            ["keys"] = new(1, 1, a => Object(a[0], "keys").Keys.Select(k => (object?)k).ToList()),
            ["values"] = new(1, 1, a => Object(a[0], "values").Values.ToList()),
            // [{ key, value }], for map/filter over an object.
            ["entries"] = new(1, 1, a => Pairs(a[0], "entries",
                (key, value) => new Dictionary<string, object?> { ["key"] = key, ["value"] = value })),
            // Back from [{ key, value }]; a repeated key keeps its last value.
            ["fromEntries"] = new(1, 1, a =>
            {
                var xs = ListOf(a, "fromEntries");
                var result = new Dictionary<string, object?>(xs.Count, StringComparer.Ordinal);
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    var entry = Object(xs[i], "fromEntries");
                    result[Values.Format(entry.TryGetValue("key", out var k) ? k : null)] = entry.TryGetValue("value", out var v) ? v : null;
                }
                return result;
            }),
            // Later objects win: merge(defaults, input)
            ["merge"] = new(1, Many, a =>
            {
                var merged = new Dictionary<string, object?>();
                foreach (var value in a)
                    foreach (var (key, item) in Object(value, "merge"))
                        merged[key] = item;
                return merged;
            }),
        };

        // The list argument; null is an empty list (like a missing path), text its characters.
        private static IEnumerable<object?> Items(object?[] arguments, string name)
        {
            var items = Values.Unwrap(arguments[0]) switch
            {
                null => [],
                IList list => list.Cast<object?>(),
                string text => text.Select(c => (object?)c.ToString()),
                IDictionary => throw new InvalidOperationException($"'{name}' needs a list, not an object; use entries() or values()."),
                var v => throw new InvalidOperationException($"'{name}' needs a list, not {Values.Describe(v)}.")
            };
            return items.Select(x =>
            {
                Budget.Spend(1);
                return x;
            });
        }

        private static ScriptFunction Fn(object?[] arguments, int index, string name)
            => arguments[index] as ScriptFunction
               ?? throw new InvalidOperationException($"'{name}' needs a function as argument {index + 1}, like x => x.name.");

        // The lambda's value for an item, or the item itself when there is no lambda.
        private static object? Key(object?[] arguments, int index, string name, object? item, int position)
            => arguments.Length > index ? Fn(arguments, index, name).Invoke(item, position) : item;

        private static ScriptFunction? OptionalFn(object?[] arguments, int index, string name)
            => arguments.Length > index ? Fn(arguments, index, name) : null;

        // first/last: the edge item, or the edge one the lambda holds true for; null if none.
        private static object? Edge(object?[] arguments, string name, bool fromEnd)
        {
            var (xs, fn) = (ListOf(arguments, name), OptionalFn(arguments, 1, name));
            for (int step = 0; step < xs.Count; step++)
            {
                int i = fromEnd ? xs.Count - 1 - step : step;
                Budget.Spend(1);
                if (fn is null || Values.ToBool(fn.Invoke(xs[i], i)))
                    return xs[i];
            }
            return null;
        }

        // xs[from..to), clamped.
        private static List<object?> Range(IList xs, int from, int to)
        {
            from = Math.Clamp(from, 0, xs.Count);
            to = Math.Clamp(to, from, xs.Count);
            Budget.Spend(to - from);
            var result = new List<object?>(to - from);
            for (int i = from; i < to; i++)
                result.Add(xs[i]);
            return result;
        }

        private static List<object?> Pairs(object? value, string name, Func<string, object?, object?> pair)
        {
            var dict = Object(value, name);
            Budget.Spend(dict.Count);
            var result = new List<object?>(dict.Count);
            foreach (var (key, item) in dict)
                result.Add(pair(key, item));
            return result;
        }

        // The list argument for indexed loops; null is empty, text its characters.
        private static IList ListOf(object?[] arguments, string name) => Values.Unwrap(arguments[0]) switch
        {
            null => Array.Empty<object?>(),
            IList list => list,
            string text => text.Select(c => (object?)c.ToString()).ToList(),
            IDictionary => throw new InvalidOperationException($"'{name}' needs a list, not an object; use entries() or values()."),
            var v => throw new InvalidOperationException($"'{name}' needs a list, not {Values.Describe(v)}.")
        };

        // Position of the first item the lambda (argument 2) holds true for, or -1.
        private static int IndexWhere(object?[] arguments, string name, out object? found)
        {
            var (xs, fn) = (ListOf(arguments, name), Fn(arguments, 1, name));
            for (int i = 0; i < xs.Count; i++)
            {
                Budget.Spend(1);
                if (Values.ToBool(fn.Invoke(xs[i], i)))
                {
                    found = xs[i];
                    return i;
                }
            }
            found = null;
            return -1;
        }

        // some/every: stops at the first item (or lambda value) whose truth is stopAt.
        private static bool Truths(object?[] arguments, string name, bool stopAt, bool whenStopped, bool otherwise)
        {
            var xs = ListOf(arguments, name);
            var fn = arguments.Length > 1 ? Fn(arguments, 1, name) : null;
            for (int i = 0; i < xs.Count; i++)
            {
                Budget.Spend(1);
                if (Values.ToBool(fn is null ? xs[i] : fn.Invoke(xs[i], i)) == stopAt)
                    return whenStopped;
            }
            return otherwise;
        }

        // The items' (or the lambda's) numbers added up, the lambda's read without boxing.
        private static double SumOf(object?[] arguments, string name, out int count)
        {
            var xs = ListOf(arguments, name);
            var fn = arguments.Length > 1 ? Fn(arguments, 1, name) : null;
            double sum = 0;
            for (int i = 0; i < xs.Count; i++)
            {
                Budget.Spend(1);
                sum += fn is null ? Values.ToNumber(xs[i]) : fn.InvokeNumber(xs[i], i);
            }
            count = xs.Count;
            return sum;
        }

        // Keys are typed once, before sorting: all text sorts ordinally, all
        // numbers (numeric text and bools count, as with <) by value; a list
        // that cannot be ordered is reported up front. See Sorting for the
        // algorithms and the order of nulls and NaN.
        private static List<object?> Sorted(object?[] arguments, string name, bool descending)
        {
            var items = ListOf(arguments, name);
            var fn = arguments.Length > 1 ? Fn(arguments, 1, name) : null;
            int n = items.Count;
            Budget.Spend(n);

            // An arithmetic key (u => u.price * u.qty) goes straight to numbers, unboxed.
            if (fn is { ReturnsNumber: true })
            {
                var computed = new double?[n];
                for (int i = 0; i < n; i++)
                    computed[i] = fn.InvokeNumber(items[i], i);
                return Arrange(items, Sorting.Numbers(computed, descending));
            }

            // One pass: the keys, and whether they are all text (or null).
            var keys = new object?[n];
            bool allText = true;
            for (int i = 0; i < n; i++)
            {
                var key = Values.Unwrap(fn is null ? items[i] : fn.Invoke(items[i], i));
                keys[i] = key;
                allText &= key is null or string;
            }

            if (allText)
                return Arrange(items, Sorting.Texts([.. keys.Select(k => (string?)k)], descending));

            var numbers = new double?[n];
            for (int i = 0; i < n; i++)
            {
                if (keys[i] is null)
                    continue;
                if (!Values.TryNumber(keys[i], out var number))
                {
                    var other = keys.First(k => k is not null and not string || Values.TryNumber(k, out _));
                    throw new InvalidOperationException(
                        $"'{name}' cannot order {Values.Describe(keys[i])} and {Values.Describe(other)}: sort numbers or texts, not both.");
                }
                numbers[i] = number;
            }
            return Arrange(items, Sorting.Numbers(numbers, descending));
        }

        private static List<object?> Arrange(IList items, int[] order)
        {
            var sorted = new List<object?>(order.Length);
            foreach (var i in order)
                sorted.Add(items[i]);
            return sorted;
        }

        private static List<object?> Splice(object?[] arguments)
        {
            var list = Items(arguments, "splice").ToList();
            var start = Values.ToNumber(arguments[1]);
            int from = (int)Math.Clamp(start < 0 ? list.Count + start : start, 0, list.Count);
            int remove = arguments.Length > 2 ? Math.Min(Count(arguments[2]), list.Count - from) : list.Count - from;
            list.RemoveRange(from, remove);
            list.InsertRange(from, arguments.Skip(3));
            return list;
        }

        private static int Count(object? value)
            => Math.Max(0, (int)Math.Min(int.MaxValue, Values.ToNumber(value)));

        private static List<object?> Range(object?[] arguments)
        {
            double start = arguments.Length > 1 ? Values.ToNumber(arguments[0]) : 0;
            double end = Values.ToNumber(arguments[arguments.Length > 1 ? 1 : 0]);
            double step = arguments.Length > 2 ? Values.ToNumber(arguments[2]) : 1;
            if (step == 0 || double.IsNaN(step))
                throw new InvalidOperationException("'range' needs a non-zero step.");

            var list = new List<object?>();
            for (double x = start; step > 0 ? x < end : x > end; x += step)
            {
                Budget.Spend(1);
                list.Add(x);
            }
            return list;
        }

        private static IDictionary<string, object?> Object(object? value, string name) => Values.Unwrap(value) switch
        {
            null => new Dictionary<string, object?>(),
            IDictionary<string, object?> dict => dict,
            IReadOnlyDictionary<string, object?> dict => dict.ToDictionary(),
            var v => throw new InvalidOperationException($"'{name}' needs an object, not {Values.Describe(v)}.")
        };
    }
}
