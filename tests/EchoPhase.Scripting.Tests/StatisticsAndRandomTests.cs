// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>median, percentile, mode, variance, stdev; rand, randInt, choice, shuffle, randomBytes, uuid4, uuid7.</summary>
    public class StatisticsAndRandomTests
    {
        private static object? Run(string expression, Dictionary<string, object?>? vars = null)
            => Eval.EvaluateExpression(expression, vars ?? []);

        private static string Text(string expression, Dictionary<string, object?>? vars = null)
            => Eval.TypedExecute<string>(expression, vars ?? []);

        // Python's statistics docs: [2, 4, 4, 4, 5, 5, 7, 9] has pvariance 4, pstdev 2.
        [Theory]
        [InlineData("pvariance([2, 4, 4, 4, 5, 5, 7, 9])", 4)]
        [InlineData("pstdev([2, 4, 4, 4, 5, 5, 7, 9])", 2)]
        [InlineData("variance([2, 4, 4, 4, 5, 5, 7, 9])", 32.0 / 7)]
        [InlineData("median([1, 3, 5])", 3)]
        [InlineData("median([1, 3, 5, 7])", 4)]
        [InlineData("median([7])", 7)]
        [InlineData("percentile([1, 2, 3, 4], 0)", 1)]
        [InlineData("percentile([1, 2, 3, 4], 100)", 4)]
        [InlineData("percentile([1, 2, 3, 4], 50)", 2.5)]
        [InlineData("percentile([10, 20, 30, 40, 50], 90)", 46)]
        [InlineData("median([{ a: 3 }, { a: 1 }, { a: 2 }], x => x.a)", 2)]
        [InlineData("variance([1e9 + 4, 1e9 + 7, 1e9 + 13, 1e9 + 16])", 30)]
        public void Statistics_MatchKnownValues(string expression, double expected)
            => Assert.Equal(expected, Eval.TypedExecute<double>(expression, new Dictionary<string, object?>()), 9);

        [Fact]
        public void Statistics_OfTooFewValues_AreNull()
        {
            Assert.Null(Run("median([])"));
            Assert.Null(Run("variance([5])"));
            Assert.Equal(0d, Run("pvariance([5])"));
            Assert.Null(Run("mode([])"));
            Assert.Throws<InvalidOperationException>(() => Run("percentile([1], 101)"));
        }

        [Fact]
        public void Mode_IsTheMostCommon_FirstOnATie()
        {
            Assert.Equal("b", Text("mode(['a', 'b', 'b', 'c'])"));
            Assert.Equal("a", Text("mode(['a', 'b'])"));
            Assert.Equal("b", Text("mode(['b', 'a', 'a', 'b'])"));
            Assert.Equal("1", Text("mode([1, '1', 2])"));
        }

        [Fact]
        public void MedianAndPercentile_AgreeWithSorting_OnRandomData()
        {
            var random = new Random(3);
            for (int round = 0; round < 200; round++)
            {
                var xs = Enumerable.Range(0, random.Next(1, 300)).Select(_ => (double)random.Next(-50, 50)).ToList();
                var sorted = xs.Order().ToList();
                double Expected(double q)
                {
                    double pos = q * (sorted.Count - 1);
                    int lo = (int)Math.Floor(pos);
                    return lo + 1 < sorted.Count ? sorted[lo] + (sorted[lo + 1] - sorted[lo]) * (pos - lo) : sorted[lo];
                }

                var vars = new Dictionary<string, object?> { ["xs"] = xs.Select(x => (object?)x).ToList() };
                Assert.Equal(Expected(0.5), Eval.TypedExecute<double>("median(xs)", vars), 9);
                Assert.Equal(Expected(0.9), Eval.TypedExecute<double>("percentile(xs, 90)", vars), 9);
                Assert.Equal(Expected(0.05), Eval.TypedExecute<double>("percentile(xs, 5)", vars), 9);
                Assert.Equal("[" + string.Join(",", xs) + "]", Text("xs", vars)); // the list itself is untouched
            }
        }

        [Fact]
        public void Random_StaysInRange_AndCoversIt()
        {
            var seen = new HashSet<double>();
            for (int i = 0; i < 2000; i++)
            {
                var unit = Eval.TypedExecute<double>("rand()", new Dictionary<string, object?>());
                Assert.InRange(unit, 0, 0.9999999999999999);
                seen.Add(Eval.TypedExecute<double>("randInt(1, 6)", new Dictionary<string, object?>()));
            }
            Assert.Equal([1d, 2d, 3d, 4d, 5d, 6d], seen.Order());
            Assert.Equal(-3d, Run("randInt(-3, -3)"));
            Assert.Throws<InvalidOperationException>(() => Run("randInt(5, 1)"));
            Assert.Throws<InvalidOperationException>(() => Run("randInt(1.5, 3)"));
        }

        [Fact]
        public void ChoiceAndShuffle_UseTheList()
        {
            var vars = new Dictionary<string, object?> { ["xs"] = Enumerable.Range(0, 50).Select(i => (object?)(double)i).ToList() };
            Assert.InRange(Eval.TypedExecute<double>("choice(xs)", vars), 0, 49);
            Assert.Null(Run("choice([])"));

            var shuffled = Text("shuffle(xs)", vars);
            Assert.NotEqual(Text("xs", vars), shuffled); // 1 in 50! to fail
            Assert.Equal(Text("xs", vars), Text("sort(shuffle(xs))", vars));
        }

        [Fact]
        public void RandomBytes_HaveTheAskedLengthAndEncoding()
        {
            Assert.Matches("^[0-9a-f]{32}$", Text("randomBytes(16)"));
            Assert.Equal(16, Convert.FromBase64String(Text("randomBytes(16, 'base64')")).Length);
            Assert.Matches("^[A-Za-z0-9_-]{43}$", Text("randomBytes(32, 'base64url')"));
            Assert.NotEqual(Text("randomBytes(16)"), Text("randomBytes(16)"));
            Assert.Throws<InvalidOperationException>(() => Run("randomBytes(0)"));
            Assert.Throws<InvalidOperationException>(() => Run("randomBytes(4096)"));
        }

        [Fact]
        public void Uuids_HaveTheirVersion_AndV7SortsByTime()
        {
            Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", Text("uuid4()"));
            Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", Text("uuid7()"));

            var ids = Enumerable.Range(0, 100).Select(_ => Text("uuid7()")).ToList();
            Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
            Assert.Equal(100, ids.Distinct().Count());
        }
    }
}
