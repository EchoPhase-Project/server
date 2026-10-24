// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>
    /// Performance guards that hold on any machine: bytes allocated per list
    /// item, which (unlike time) are the same on every run, OS and build. A
    /// regression such as a dictionary per lambda call again fails here, in
    /// CI. Budgets are about 1.5x what the engine allocates now; timings are
    /// in tests/EchoPhase.Scripting.Benchmarks (mise run bench).
    /// </summary>
    public class AllocationBudgetTests
    {
        private const int Items = 20_000;

        private static readonly Dictionary<string, object> Vars = new()
        {
            ["xs"] = Enumerable.Range(0, Items).Select(i => (object?)(double)(i * 7 % 1000)).ToList(),
            ["users"] = Enumerable.Range(0, Items).Select(i => (object?)new Dictionary<string, object?>
            {
                ["age"] = (double)(i % 100),
                ["name"] = "u" + i
            }).ToList(),
        };

        [Theory]
        // now: 0 B/item — the lambda's scope is reused and numbers are not boxed
        [InlineData("sum(users, u => u.age)", 2)]
        [InlineData("count(xs, x => x * 2 > 0)", 2)]
        [InlineData("median(users, u => u.age * 2)", 16)]
        // now: the result list (8 B/slot), plus a boxed number for computed ones
        [InlineData("map(xs, x => x)", 16)]
        [InlineData("map(xs, x => x * 2)", 48)]
        [InlineData("map(users, (u, i) => i + u.age)", 80)]
        [InlineData("filter(users, u => u.age >= 18)", 40)]
        [InlineData("[u.name for u in users if u.age >= 18]", 40)]
        // now: 128 — a function value and a scope per outer item
        [InlineData("count(users, u => some([1, 2], d => u.age % d == 0))", 192)]
        // now: 72 / 89 — keys, order and result arrays
        [InlineData("sort(xs)", 110)]
        [InlineData("sort(users, u => u.name)", 135)]
        public void Evaluation_StaysWithinItsAllocationBudget(string expression, int bytesPerItem)
        {
            // Warm up: parse once, let the JIT settle.
            for (int i = 0; i < 20; i++)
                Eval.TypedExecute<object>(expression, Vars);

            long least = long.MaxValue;
            for (int run = 0; run < 3; run++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                Eval.TypedExecute<object>(expression, Vars);
                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }

            double perItem = least / (double)Items;
            Assert.True(perItem <= bytesPerItem, $"{expression}: {perItem:F1} B/item, budget {bytesPerItem}.");
        }
    }
}
