// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A workflow as it was at one version: written whenever its definition
    /// changes, so earlier programs can be viewed and restored.
    /// </summary>
    public class WorkflowVersion
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        public Guid WorkflowId { get; set; }
        public Workflow? Workflow { get; set; }

        public int Version { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>The program as JSON (jsonb), as in <see cref="Workflow.Definition"/>.</summary>
        public string Definition { get; set; } = "{}";

        /// <summary>SHA-256 (hex) of the program's canonical JSON; null for versions saved before hashes.</summary>
        public string? ContentHash { get; set; }

        /// <summary>The version it was made from (the one current when it was saved); null for the first.</summary>
        public int? ParentVersion { get; set; }

        /// <summary>When it brought back an earlier version: that version.</summary>
        public int? RestoredFrom { get; set; }

        /// <summary>Who saved it; differs from the owner under impersonation.</summary>
        public Guid? SavedBy { get; set; }

        /// <summary>UTC.</summary>
        public DateTime CreatedAt { get; set; }
    }
}
