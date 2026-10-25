// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>bisectLeft, bisectRight, binarySearch and toSet.</summary>
    public class SearchTests
    {
        private static object? Run(string expression, Dictionary<string, object?>? vars = null)
            => Eval.EvaluateExpression(expression, vars ?? []);

        private static int Int(string expression, Dictionary<string, object?> vars)
            => Eval.TypedExecute<int>(expression, vars);

        private static object Value(string expression, Dictionary<string, object?>? vars = null)
        {
            var value = Run(expression, vars);
            Assert.NotNull(value);
            return value;
        }

        [Fact]
        public void Numbers_MatchALinearScan()
        {
            var random = new Random(11);
            double?[] pool = [null, double.NaN, -2, 0, 1, 1, 3, 5, 5, 5, 8];
            for (int round = 0; round < 300; round++)
            {
                var vars = new Dictionary<string, object?>
                {
                    ["xs"] = Value("sort(raw)", new() { ["raw"] = Enumerable.Range(0, random.Next(0, 40)).Select(_ => (object?)pool[random.Next(pool.Length)]).ToList() })
                };
                var sorted = Assert.IsType<List<object?>>(vars["xs"]);
                foreach (var probe in new double?[] { null, double.NaN, -3, -2, 0, 1, 2, 5, 8, 9 })
                {
                    // null is searched for as a literal: a variable may not hold it.
                    if (probe is { } number)
                        vars["v"] = number;
                    else
                        vars.Remove("v");
                    int Rank(object? x) => x is null ? 0 : double.IsNaN((double)x) ? 1 : 2;
                    int Cmp(object? x)
                    {
                        object? y = probe;
                        if (Rank(x) != Rank(y) || Rank(x) < 2)
                            return Rank(x).CompareTo(Rank(y));
                        return Convert.ToDouble(x).CompareTo(Convert.ToDouble(y));
                    }

                    int left = sorted.FindIndex(x => Cmp(x) >= 0) is var l && l >= 0 ? l : sorted.Count;
                    int right = sorted.FindIndex(x => Cmp(x) > 0) is var r && r >= 0 ? r : sorted.Count;
                    var call = probe is null ? "null" : "v";
                    Assert.Equal(left, Int($"bisectLeft(xs, {call})", vars));
                    Assert.Equal(right, Int($"bisectRight(xs, {call})", vars));
                    Assert.Equal(left < right ? left : -1, Int($"binarySearch(xs, {call})", vars));
                }
            }
        }

        [Fact]
        public void Texts_SearchOrdinally_WithAKey()
        {
            var vars = new Dictionary<string, object?>
            {
                ["users"] = Value("sort([{ n: 'bob' }, { n: 'Ann' }, { n: 'cid' }, { n: 'bob' }], u => u.n)")
            };
            Assert.Equal(1, Int("binarySearch(users, 'bob', u => u.n)", vars));
            Assert.Equal(3, Int("bisectRight(users, 'bob', u => u.n)", vars));
            Assert.Equal(-1, Int("binarySearch(users, 'ann', u => u.n)", vars));
            Assert.Equal(0, Int("bisectLeft(users, 'A', u => u.n)", vars));
        }

        public sealed class Counted(double value, Action seen)
        {
            public double Value
            {
                get
                {
                    seen();
                    return value;
                }
            }
        }

        [Fact]
        public void Keys_AreComputedOnlyForProbedElements()
        {
            int reads = 0;
            var items = Enumerable.Range(0, 100_000).Select(i => (object?)new Counted(i, () => reads++)).ToList();
            var vars = new Dictionary<string, object?> { ["xs"] = items };

            Assert.Equal(76_543, Int("binarySearch(xs, 76543, x => x.Value)", vars));
            Assert.InRange(reads, 1, 20); // ~log2(100 000) = 17, not 100 000
        }

        [Fact]
        public void MixingTypes_IsAnError_NotAWrongAnswer()
        {
            var numbers = new Dictionary<string, object?> { ["xs"] = new List<object?> { 1L, 2L, 3L } };
            Assert.Throws<InvalidOperationException>(() => Run("binarySearch(xs, 'b')", numbers));
            var texts = new Dictionary<string, object?> { ["xs"] = new List<object?> { "a", "b" } };
            Assert.Throws<InvalidOperationException>(() => Run("binarySearch(xs, 1)", texts));
            Assert.Equal(0, Int("bisectLeft([], 5)", numbers));
            Assert.Equal(1d, Run("binarySearch([1, '2', 10], 2)"));
        }

        [Fact]
        public void ToSet_GivesConstantTimeMembership()
        {
            var vars = new Dictionary<string, object?>
            {
                ["ids"] = new List<object?> { 3L, 5L, 8L },
                ["users"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["id"] = 5L }, new Dictionary<string, object?> { ["id"] = 6L },
                },
            };
            Assert.Equal("[5]", Eval.TypedExecute<string>("[u.id for u in users if u.id in toSet(ids)]", vars));
            Assert.Equal(true, Run("'5' in toSet(ids)", vars));
            Assert.Equal(true, Run("6 in toSet(users, u => u.id)", vars));
        }
    }
}
