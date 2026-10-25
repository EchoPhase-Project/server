// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using BenchmarkDotNet.Attributes;

namespace EchoPhase.Scripting.Benchmarks
{
    /// <summary>Data shared by the groups: numbers and user-like objects.</summary>
    public abstract class ExpressionBenchmark
    {
        [Params(1_000, 100_000)]
        public int Items { get; set; }

        protected Dictionary<string, object?> Vars { get; private set; } = [];

        [GlobalSetup]
        public void Setup()
        {
            var random = new Random(1);
            Vars = new Dictionary<string, object?>
            {
                ["xs"] = Enumerable.Range(0, Items).Select(_ => (object?)(double)random.Next(1_000_000)).ToList(),
                ["users"] = Enumerable.Range(0, Items).Select(i => (object?)new Dictionary<string, object?>
                {
                    ["age"] = (double)random.Next(100),
                    ["name"] = "u" + i,
                    ["k"] = i * 2d
                }).ToList(),
                ["qs"] = Enumerable.Range(0, 1000).Select(_ => (object?)(double)random.Next(0, 2 * Items)).ToList(),
            };
            Vars["set"] = Eval.EvaluateExpression("toSet(xs)", Vars)
                ?? throw new InvalidOperationException("toSet gave no value.");
        }

        protected object? Run(string expression) => Eval.EvaluateExpression(expression, Vars);
    }

    [MemoryDiagnoser]
    public class Lambdas : ExpressionBenchmark
    {
        [Benchmark] public object? MapIdentity() => Run("map(xs, x => x)");
        [Benchmark] public object? MapArithmetic() => Run("map(xs, x => x * 2)");
        [Benchmark] public object? MapWithIndex() => Run("map(users, (u, i) => i + u.age)");
        [Benchmark] public object? Filter() => Run("filter(users, u => u.age >= 18)");
        [Benchmark] public object? Sum() => Run("sum(users, u => u.age)");
        [Benchmark] public object? CountArithmetic() => Run("count(xs, x => x * 2 > 0)");
        [Benchmark] public object? Comprehension() => Run("[u.name for u in users if u.age >= 18]");
        [Benchmark] public object? Nested() => Run("count(users, u => some([1, 2], d => u.age % d == 0))");
    }

    [MemoryDiagnoser]
    public class Sorting : ExpressionBenchmark
    {
        [Benchmark] public object? Numbers() => Run("sort(xs)");
        [Benchmark] public object? ByText() => Run("sort(users, u => u.name)");
        [Benchmark] public object? ByField() => Run("sort(users, u => u.age)");
        [Benchmark] public object? ByArithmetic() => Run("sort(users, u => u.age * 2 - u.k)");
        [Benchmark] public object? Median() => Run("median(users, u => u.age * 2)");
    }

    [MemoryDiagnoser]
    public class Searching : ExpressionBenchmark
    {
        [Benchmark] public object? BinarySearch() => Run("[binarySearch(users, q, u => u.k) for q in qs]");
        [Benchmark] public object? InSet() => Run("[q in set for q in qs]");
    }
}
