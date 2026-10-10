// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A saved block program. Triggers in it (webhook, schedule) start runs
    /// only while it is active; a manual run works either way.
    /// </summary>
    public class Workflow : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        public Guid UserId { get; set; }
        public User? User { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>The program (blocks, connections, settings) as JSON (jsonb).</summary>
        public string Definition { get; set; } = "{}";

        public bool Active { get; set; }

        /// <summary>Bumped on every change of the definition.</summary>
        public int Version { get; set; } = 1;

        public ICollection<WorkflowSchedule> Schedules { get; set; } = new List<WorkflowSchedule>();
        public ICollection<WorkflowVersion> Versions { get; set; } = new List<WorkflowVersion>();

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
