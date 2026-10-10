// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A non-interactive client (bot, script, server) that acts on behalf of its
    /// owner using the OAuth client-credentials grant. <see cref="Id"/> is the
    /// public client id; the secret is stored only as a hash.
    /// </summary>
    public class ApiClient : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        [Expose] public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        [Expose] public Guid UserId { get; set; }
        public User? User { get; set; }

        [Expose] public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Hex HMAC-SHA256 of the current secret.
        /// </summary>
        public string SecretHash { get; set; } = string.Empty;

        /// <summary>
        /// Hash of the secret replaced by the last rotation; still accepted until
        /// removed so instances can switch over without downtime.
        /// </summary>
        public string? PreviousSecretHash { get; set; }

        [Expose] public ICollection<ApiClientPermissionEntry> Permissions { get; set; } = [];

        [Expose] public DateTime? ExpiresAt { get; set; }
        [Expose] public DateTime? LastUsedAt { get; set; }
        [Expose] public DateTime? SecretRotatedAt { get; set; }

        [Expose] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        [Expose] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [ConcurrencyCheck] public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }

    public class ApiClientPermissionEntry : IIdentifiable
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        public Guid ApiClientId { get; set; }
        public ApiClient ApiClient { get; set; } = default!;
        [Expose] public string Resource { get; set; } = string.Empty;
        [Expose] public string[] Permissions { get; set; } = [];
    }
}
