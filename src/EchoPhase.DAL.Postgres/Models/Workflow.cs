// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A saved block program, always in a package. Triggers in it (webhook,
    /// schedule) start runs only while it is active; a manual run works either
    /// way.
    /// </summary>
    public class Workflow : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        public Guid UserId { get; set; }
        public User? User { get; set; }

        /// <summary>The package it lives in: the owner's, like the workflow.</summary>
        public Guid PackageId { get; set; }
        public WorkflowPackage? Package { get; set; }

        /// <summary>
        /// Its name for the package's other workflows (ExecuteWorkflow, the
        /// error workflow): unique in the package, kept by renames, the same in
        /// an installed copy, so the copy's programs need no rewriting.
        /// </summary>
        public Guid Key { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        /// <summary>The program (blocks, connections, settings) as JSON (jsonb).</summary>
        public string Definition { get; set; } = "{}";

        public bool Active { get; set; }

        /// <summary>Bumped on every change of the definition.</summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// SHA-256 (hex) of the program's canonical JSON (BlockProgramHash); null
        /// for workflows saved before hashes, until their next save.
        /// </summary>
        public string? ContentHash { get; set; }


        public ICollection<WorkflowSchedule> Schedules { get; set; } = new List<WorkflowSchedule>();
        public ICollection<WorkflowVersion> Versions { get; set; } = new List<WorkflowVersion>();

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
