// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A user's account on an external provider that EchoPhase may call on
    /// their behalf.
    /// </summary>
    public class Connection : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        public Guid UserId { get; set; }
        public User? User { get; set; }

        public string Provider { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ConnectionAuthType AuthType { get; set; }
        public ConnectionStatus Status { get; set; } = ConnectionStatus.Active;

        /// <summary>
        /// The OAuth application the tokens were issued to; <c>null</c> means
        /// the platform application from configuration.
        /// </summary>
        public Guid? OAuthAppId { get; set; }
        public OAuthApp? OAuthApp { get; set; }

        public string? ExternalAccountId { get; set; }
        public string? ExternalLogin { get; set; }
        public string[] Scopes { get; set; } = [];

        /// <summary>
        /// Encrypted token payload. Never exposed, never stored in plain text.
        /// </summary>
        public byte[] Secret { get; set; } = [];

        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastValidatedAt { get; set; }
        public string? LastError { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
