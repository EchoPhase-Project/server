// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// Actions on one resource given to a user on top of their roles. One row
    /// per (user, resource); granting again replaces it. Role-only actions
    /// are never stored here.
    /// </summary>
    public class UserPermissionGrant : ITrackingEntity, IIdentifiable
    {
        [Expose] public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);

        [Expose] public Guid UserId { get; set; }
        public User? User { get; set; }

        [Expose] public string Resource { get; set; } = string.Empty;
        [Expose] public string[] Permissions { get; set; } = [];

        /// <summary>Who granted it; null once that user is deleted.</summary>
        [Expose] public Guid? GrantedById { get; set; }
        public User? GrantedBy { get; set; }

        [Expose] public string Reason { get; set; } = string.Empty;

        /// <summary>Null: until revoked.</summary>
        [Expose] public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Set once the expiry has revoked the user's tokens, so it happens once.
        /// </summary>
        public bool ExpiryApplied { get; set; }

        [Expose] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [Expose] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive(DateTime now) => ExpiresAt is null || ExpiresAt > now;
    }
}
