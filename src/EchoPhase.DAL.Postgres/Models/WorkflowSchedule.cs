// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// When a ScheduleTrigger of an active workflow fires next. Derived from
    /// the definition on save; the scheduler moves NextRunAt after each run.
    /// </summary>
    public class WorkflowSchedule
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        public Guid WorkflowId { get; set; }
        public Workflow? Workflow { get; set; }

        /// <summary>The ScheduleTrigger block.</summary>
        public int BlockId { get; set; }

        public string Cron { get; set; } = string.Empty;

        /// <summary>IANA zone the cron is read in.</summary>
        public string TimeZone { get; set; } = "UTC";

        /// <summary>UTC.</summary>
        public DateTime NextRunAt { get; set; }
    }
}
