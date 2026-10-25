// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Triggers;

namespace EchoPhase.Runners.Tests
{
    public class CronScheduleTests
    {
        [Fact]
        public void ReadsTheCronInTheZone()
        {
            // 9:00 in Moscow (UTC+3, no DST) is 06:00 UTC.
            var next = CronSchedule.Next("0 9 * * *", "Europe/Moscow", new DateTimeOffset(2026, 10, 9, 7, 0, 0, TimeSpan.Zero));

            Assert.Equal(new DateTimeOffset(2026, 10, 10, 6, 0, 0, TimeSpan.Zero), next);
        }

        [Fact]
        public void KeepsLocalTimeAcrossDst()
        {
            // Berlin leaves summer time on 25 October 2026: 9:00 local is 07:00 UTC before, 08:00 UTC after.
            var before = CronSchedule.Next("0 9 * * *", "Europe/Berlin", new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));
            var after = CronSchedule.Next("0 9 * * *", "Europe/Berlin", new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));

            Assert.Equal(new DateTimeOffset(2026, 10, 24, 7, 0, 0, TimeSpan.Zero), before);
            Assert.Equal(new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero), after);
        }

        [Fact]
        public void SkippedLocalTime_FiresWhenTheClocksJump()
        {
            // 29 March 2026 in Berlin: 02:00-03:00 does not exist; 02:30 fires at the jump (01:00 UTC).
            var next = CronSchedule.Next("30 2 * * *", "Europe/Berlin", new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));

            Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero), next);
        }

        [Fact]
        public void RepeatedLocalTime_FiresOnce()
        {
            // 25 October 2026 in Berlin: 02:30 happens twice; one run, at the first (00:30 UTC).
            var first = CronSchedule.Next("30 2 * * *", "Europe/Berlin", new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
            var second = CronSchedule.Next("30 2 * * *", "Europe/Berlin", Assert.NotNull(first));

            Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), first);
            Assert.Equal(new DateTimeOffset(2026, 10, 26, 1, 30, 0, TimeSpan.Zero), second);
        }
    }
}
