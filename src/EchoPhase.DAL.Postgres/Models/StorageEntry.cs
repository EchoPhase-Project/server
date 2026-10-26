// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A value a workflow keeps between runs (the Storage block): one
    /// workflow's own (WorkflowId set) or shared by the workflows of its
    /// package (WorkflowId null). Goes with its workflow or package.
    /// </summary>
    public class StorageEntry : ITrackingEntity, IIdentifiable
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);

        public Guid UserId { get; set; }
        public User? User { get; set; }

        /// <summary>The package it belongs to.</summary>
        public Guid PackageId { get; set; }
        public WorkflowPackage? Package { get; set; }

        /// <summary>The workflow it belongs to; null: shared by the package's workflows.</summary>
        public Guid? WorkflowId { get; set; }
        public Workflow? Workflow { get; set; }

        public string Key { get; set; } = string.Empty;

        /// <summary>The value as JSON.</summary>
        public string Value { get; set; } = "null";

        /// <summary>Bytes of Value (UTF-8), for the user's quota without reading values.</summary>
        public int Size { get; set; }

        /// <summary>When it stops existing; null: kept until deleted.</summary>
        public DateTime? ExpiresAt { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
