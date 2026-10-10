// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Cronos;

namespace EchoPhase.Runners.Blocks.Triggers
{
    /// <summary>When a ScheduleTrigger fires.</summary>
    public static class CronSchedule
    {
        /// <summary>
        /// The next time strictly after <paramref name="after"/>, the cron read
        /// in the IANA <paramref name="zone"/>. Cronos handles DST: a local time
        /// the clocks skip fires at the moment they jump, a repeated one once.
        /// Null when it never fires again.
        /// </summary>
        public static DateTimeOffset? Next(string cron, string zone, DateTimeOffset after)
        {
            var expression = CronExpression.Parse(cron, CronFormat.Standard);
            var timeZone = TimeZoneInfo.TryFindSystemTimeZoneById(zone, out var tz) ? tz : TimeZoneInfo.Utc;
            return expression.GetNextOccurrence(after, timeZone);
        }
    }
}
