// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Scripting.Engine;

namespace EchoPhase.Scripting.Tests
{
    public class ParseCacheTests
    {
        [Fact]
        public void HotSources_StayParsed_WhileColdOnesComeAndGo()
        {
            long time = 0;
            var parses = new Dictionary<string, int>();
            var cache = new ParseCache<string>(8, s =>
            {
                parses[s] = parses.GetValueOrDefault(s) + 1;
                return s.ToUpperInvariant();
            }, () => time);

            for (int i = 0; i < 1000; i++)
            {
                time++;
                Assert.Equal("HOT", cache.Get("hot"));
                time++;
                cache.Get($"cold-{i}");
            }

            Assert.Equal(1, parses["hot"]);
            Assert.InRange(cache.Count, 1, 8);
        }

        [Fact]
        public void FailedParses_AreNotKept()
        {
            int calls = 0;
            var cache = new ParseCache<string>(8, s => { calls++; throw new FormatException(s); });
            Assert.Throws<FormatException>(() => cache.Get("bad"));
            Assert.Throws<FormatException>(() => cache.Get("bad"));
            Assert.Equal(2, calls);
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public void ManyThreads_KeepTheBound()
        {
            // Evictions race with adds on other threads (this once caught a real race).
            var cache = new ParseCache<string>(64, s => s);
            for (int round = 0; round < 20; round++)
                Parallel.For(0, 20_000, new ParallelOptions { MaxDegreeOfParallelism = 8 },
                    i => Assert.Equal($"s{i % 500}", cache.Get($"s{i % 500}")));
            Assert.InRange(cache.Count, 1, 64);
        }
    }
}
