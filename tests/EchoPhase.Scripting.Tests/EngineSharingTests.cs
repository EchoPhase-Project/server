// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>What the engine shares between evaluations must never leak between them: folded literals, pooled arguments.</summary>
    public class EngineSharingTests
    {
        [Fact]
        public void ConstantLiterals_AreNeverChangedBySet()
        {
            var vars = new Dictionary<string, object>();
            Eval.Set(vars, "a", Eval.Evaluate("@([1, 2])", vars));
            Eval.Set(vars, "a[0]", 9L);
            Eval.Set(vars, "o", Eval.Evaluate("@({ x: [1, 2], y: { z: 1 } })", vars));
            Eval.Set(vars, "o.x[1]", 7L);
            Eval.Set(vars, "o.y.z", 5L);

            Assert.Equal("[9,2]", Eval.TypedExecute<string>("a", vars));
            Assert.Equal("{\"x\":[1,7],\"y\":{\"z\":5}}", Eval.TypedExecute<string>("o", vars));
            Assert.Equal("[1,2]", Eval.TypedExecute<string>("[1, 2]", vars));
            Assert.Equal("{\"x\":[1,2],\"y\":{\"z\":1}}", Eval.TypedExecute<string>("{ x: [1, 2], y: { z: 1 } }", vars));
        }

        [Fact]
        public void NestedCalls_OfTheSameArity_KeepTheirOwnArguments()
        {
            var vars = new Dictionary<string, object>();
            Assert.Equal("[4,8,12]", Eval.TypedExecute<string>("map([1, 2, 3], x => sum(map([x, x], y => y * 2)))", vars));
            Assert.Equal("[\"a-1\",\"b-2\"]", Eval.TypedExecute<string>(
                "map(zip(['a', 'b'], [1, 2]), p => join([p[0], str(p[1])], '-'))", vars));
            Assert.Equal(6d, Eval.TypedExecute<object>("reduce([1, 2, 3], (acc, x) => acc + max(x, min(x, 10)), 0)", vars));
        }

        [Fact]
        public void TheSameExpression_OnManyThreads_GivesEachItsOwnAnswer()
        {
            var failures = 0;
            Parallel.For(0, 2000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var vars = new Dictionary<string, object> { ["n"] = (double)i };
                var result = Eval.TypedExecute<string>("join(map([n, n + 1, n + 2], x => str(max(x, 0) * 2)), ',')", vars);
                if (result != $"{2 * i},{2 * i + 2},{2 * i + 4}")
                    Interlocked.Increment(ref failures);
            });
            Assert.Equal(0, failures);
        }
    }
}
