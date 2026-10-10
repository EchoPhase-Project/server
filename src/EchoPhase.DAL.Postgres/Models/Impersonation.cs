// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A privileged user acting as another user (support, debugging). Tokens
    /// issued for it carry an <c>act</c> claim and die when this record ends,
    /// expires, or the actor's security stamp changes.
    /// </summary>
    public class Impersonation : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        [Expose] public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);

        [Expose] public Guid ActorId { get; set; }
        public User? Actor { get; set; }

        [Expose] public Guid TargetId { get; set; }
        public User? Target { get; set; }

        [Expose] public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// The actor's stamp at start; a role or password change of the actor
        /// therefore ends the impersonation as well.
        /// </summary>
        public string ActorSecurityStamp { get; set; } = string.Empty;

        [Expose] public DateTime ExpiresAt { get; set; }
        [Expose] public DateTime? EndedAt { get; set; }

        [Expose] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [Expose] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [ConcurrencyCheck] public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
