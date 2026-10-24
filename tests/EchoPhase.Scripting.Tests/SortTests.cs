// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;

namespace EchoPhase.Scripting.Tests
{
    /// <summary>
    /// sort() and sortDesc() (radix for numbers, dual-pivot quicksort for
    /// text) against LINQ's stable OrderBy as the reference.
    /// </summary>
    public class SortTests
    {
        public static TheoryData<int> Sizes => [0, 1, 2, 5, 31, 32, 33, 100, 1000, 5000];

        private static List<string> Sort(List<object?> items, bool descending)
        {
            var vars = new Dictionary<string, object> { ["xs"] = items };
            var sorted = Eval.TypedExecute<List<object?>>(
                descending ? "sortDesc(xs, x => x.k)" : "sort(xs, x => x.k)", vars);
            return sorted.Select(x => Assert.IsType<string>(Assert.IsType<Dictionary<string, object?>>(x)["id"])).ToList();
        }

        private static List<object?> Items<T>(IEnumerable<T> keys)
            => keys.Select((k, i) => (object?)new Dictionary<string, object?> { ["k"] = k, ["id"] = i.ToString() }).ToList();

        private static List<string> Ids<T>(IEnumerable<(T Key, int Index)> ordered)
            => ordered.Select(p => p.Index.ToString()).ToList();

        [Theory]
        [MemberData(nameof(Sizes))]
        public void Numbers_MatchAStableSort(int size)
        {
            var random = new Random(size);
            // Few distinct values (many ties), nulls, NaN, -0 and infinities.
            double?[] pool = [null, double.NaN, -0.0, 0.0, 1.5, -1.5, 2, 3, -7, double.PositiveInfinity, double.NegativeInfinity, 1e300, -1e-300];
            var keys = Enumerable.Range(0, size).Select(_ => pool[random.Next(pool.Length)]).ToList();
            var indexed = keys.Select((k, i) => (Key: k, Index: i)).ToList();

            Assert.Equal(Ids(indexed.OrderBy(p => p.Key, Comparer<double?>.Default)), Sort(Items(keys), false));
            Assert.Equal(Ids(indexed.OrderByDescending(p => p.Key, Comparer<double?>.Default)), Sort(Items(keys), true));
        }

        [Theory]
        [MemberData(nameof(Sizes))]
        public void Texts_MatchAStableSort(int size)
        {
            var random = new Random(size);
            string? Next() => random.Next(10) == 0 ? null : new string(Enumerable.Range(0, random.Next(4)).Select(_ => "aAbB_é"[random.Next(6)]).ToArray());
            var keys = Enumerable.Range(0, size).Select(_ => Next()).ToList();
            var indexed = keys.Select((k, i) => (Key: k, Index: i)).ToList();

            Assert.Equal(Ids(indexed.OrderBy(p => p.Key, StringComparer.Ordinal)), Sort(Items(keys), false));
            Assert.Equal(Ids(indexed.OrderByDescending(p => p.Key, StringComparer.Ordinal)), Sort(Items(keys), true));
        }

        [Theory]
        [InlineData("sorted")]
        [InlineData("reversed")]
        [InlineData("equal")]
        [InlineData("organ")]
        [InlineData("sawtooth")]
        [InlineData("descending ties")]
        [InlineData("sorted blocks")]
        [InlineData("nearly sorted")]
        public void Texts_HardShapes_StayCorrectAndFast(string shape)
            => HardShape(shape, descending: false);

        [Theory]
        [InlineData("descending ties")]
        [InlineData("sorted blocks")]
        [InlineData("organ")]
        public void Texts_HardShapes_Descending(string shape)
            => HardShape(shape, descending: true);

        private static void HardShape(string shape, bool descending)
        {
            const int Count = 50_000;
            var keys = Enumerable.Range(0, Count).Select(i => shape switch
            {
                "sorted" => i,
                "reversed" => Count - i,
                "equal" => 7,
                "organ" => i < Count / 2 ? i : Count - i,
                "descending ties" => (Count - i) / 10,
                "sorted blocks" => i % 1000 * 7919 % 1000003 + i / 1000,
                "nearly sorted" => i % 500 == 0 ? Count - i : i,
                _ => i % 97
            }).Select(v => v.ToString("D6")).ToList();
            var indexed = keys.Select((k, i) => (Key: (string?)k, Index: i)).ToList();

            var watch = Stopwatch.StartNew();
            var sorted = Sort(Items(keys), descending);
            watch.Stop();

            var expected = descending
                ? indexed.OrderByDescending(p => p.Key, StringComparer.Ordinal)
                : indexed.OrderBy(p => p.Key, StringComparer.Ordinal);
            Assert.Equal(Ids(expected), sorted);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"{shape}: {watch.Elapsed}");
        }

        [Theory]
        [MemberData(nameof(Sizes))]
        public void ArithmeticKeys_SortLikeTheirValues(int size)
        {
            // x.k * 1 takes the unboxed path; x.k the general one: same order, ties included.
            var random = new Random(size + 1);
            double[] pool = [double.NaN, -0.0, 0.0, 1.5, -1.5, 2, 2, 3, double.PositiveInfinity, double.NegativeInfinity];
            var vars = new Dictionary<string, object> { ["xs"] = Items(Enumerable.Range(0, size).Select(_ => pool[random.Next(pool.Length)])) };

            foreach (var sort in new[] { "sort", "sortDesc" })
                Assert.Equal(
                    Eval.TypedExecute<string>($"map({sort}(xs, x => x.k), x => x.id)", vars),
                    Eval.TypedExecute<string>($"map({sort}(xs, x => x.k * 1), x => x.id)", vars));
        }

        [Fact]
        public void PlainLists_SortByValue()
        {
            var vars = new Dictionary<string, object>();
            Assert.Equal("[null,-1,0,2.5,10]", Eval.TypedExecute<string>("sort([10, null, 2.5, -1, 0])", vars));
            Assert.Equal("[\"b\",\"a\",\"B\",null]", Eval.TypedExecute<string>("sortDesc(['a', null, 'B', 'b'])", vars));
        }
    }
}
