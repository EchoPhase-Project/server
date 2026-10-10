// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A user's account on an external provider that EchoPhase may call on
    /// their behalf.
    /// </summary>
    public class Connection : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        [Expose] public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        [Expose] public Guid UserId { get; set; }
        public User? User { get; set; }

        [Expose] public string Provider { get; set; } = string.Empty;
        [Expose] public string Name { get; set; } = string.Empty;
        [Expose] public ConnectionAuthType AuthType { get; set; }
        [Expose] public ConnectionStatus Status { get; set; } = ConnectionStatus.Active;

        /// <summary>
        /// The OAuth application the tokens were issued to; <c>null</c> means
        /// the platform application from configuration.
        /// </summary>
        [Expose] public Guid? OAuthAppId { get; set; }
        public OAuthApp? OAuthApp { get; set; }

        [Expose] public string? ExternalAccountId { get; set; }
        [Expose] public string? ExternalLogin { get; set; }
        [Expose] public string[] Scopes { get; set; } = [];

        /// <summary>
        /// Encrypted token payload. Never exposed, never stored in plain text.
        /// </summary>
        public byte[] Secret { get; set; } = [];

        [Expose] public DateTime? ExpiresAt { get; set; }
        [Expose] public DateTime? LastValidatedAt { get; set; }
        [Expose] public string? LastError { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
