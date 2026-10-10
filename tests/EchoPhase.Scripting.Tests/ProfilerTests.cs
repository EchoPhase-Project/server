// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Profilers;

namespace EchoPhase.Scripting.Tests
{
    public class ProfilerTests
    {
        [Fact]
        public void Disabled_RecordsNothing()
        {
            var profiler = new StackProfiler(enabled: false);

            using (profiler.Step("Outer"))
            using (profiler.Step("Inner"))
            {
            }

            Assert.Equal("", profiler.Dump());
        }

        [Fact]
        public void Stack_NestsStepsUnderTheirCaller()
        {
            var profiler = new StackProfiler();

            using (profiler.Step("Parse"))
            {
                using (profiler.Step("Lex")) { }
                using (profiler.Step("Lex")) { }
            }

            var lines = Lines(profiler);
            Assert.StartsWith("Parse: count=1", lines[0]);
            Assert.StartsWith("└─Lex: count=2", lines[1]);
        }

        [Fact]
        public async Task Stack_ConcurrentFlowsKeepTheirOwnPosition()
        {
            var profiler = new StackProfiler();

            // A shared stack would interleave these (or throw on dispose);
            // per-flow tracking puts each child under its own parent.
            async Task Flow(string name)
            {
                using (profiler.Step(name))
                {
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                    using (profiler.Step($"{name}.Child"))
                        await Task.Delay(10, TestContext.Current.CancellationToken);
                }
            }

            await Task.WhenAll(Flow("A"), Flow("B"));

            var lines = Lines(profiler);
            var a = Array.FindIndex(lines, l => l.StartsWith("A:"));
            var b = Array.FindIndex(lines, l => l.StartsWith("B:"));
            Assert.StartsWith("└─A.Child:", lines[a + 1]);
            Assert.StartsWith("└─B.Child:", lines[b + 1]);
            Assert.Equal(4, lines.Length);
        }

        [Fact]
        public void Stack_RecursionGetsNumberedLevels()
        {
            var profiler = new StackProfiler();

            void Recurse(int depth)
            {
                using (profiler.Step("Expr"))
                    if (depth > 0)
                        Recurse(depth - 1);
            }

            Recurse(2);

            var lines = Lines(profiler);
            Assert.StartsWith("Expr:", lines[0]);
            Assert.StartsWith("└─Expr#1:", lines[1]);
            Assert.StartsWith("  └─Expr#2:", lines[2]);
        }

        [Fact]
        public void Flat_CountsByNameAndAllocations()
        {
            var profiler = new FlatProfiler();

            for (int i = 0; i < 3; i++)
                using (profiler.Step("Alloc"))
                    _ = new byte[10_000];

            var line = Assert.Single(Lines(profiler));
            Assert.StartsWith("Alloc: count=3", line);
            Assert.DoesNotContain("allocated=0 B", line);
        }

        private static string[] Lines(IProfiler profiler)
            => profiler.Dump().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }
}
