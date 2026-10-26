// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Entities
{
    /// <summary>
    /// One execution of a block program, kept in its owner's history. The
    /// owner is the partition key, so a run can only be read through it.
    /// </summary>
    public class BlockRun
    {
        public Guid UserId { get; set; }

        /// <summary>UUIDv7: byte order is creation order, newest first in the table.</summary>
        public Guid RunId { get; set; }

        /// <summary>One of <c>BlockRunStatus</c>.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Set when the run was started under impersonation.</summary>
        public Guid? ActorId { get; set; }

        /// <summary>The saved workflow it ran; null for a program sent to run directly.</summary>
        public Guid? WorkflowId { get; set; }

        /// <summary>The package of that workflow; null for a program sent directly and for older runs.</summary>
        public Guid? PackageId { get; set; }

        /// <summary>
        /// SHA-256 (hex) of the program it ran, canonical JSON: the same as its
        /// workflow version's ContentHash when it ran one. Null for older runs.
        /// </summary>
        public string? ProgramHash { get; set; }

        /// <summary>What started it: manual, webhook or schedule.</summary>
        public string Trigger { get; set; } = "manual";

        public int BlockCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public long? DurationMs { get; set; }
        public int? Steps { get; set; }
        public int? ErrorCount { get; set; }
    }
}
