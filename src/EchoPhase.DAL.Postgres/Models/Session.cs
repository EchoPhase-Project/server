// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A signed-in device or API client. <see cref="Id"/> is a public
    /// identifier (listing, revoking); the refresh token is the credential and
    /// is stored only as a hash.
    /// </summary>
    public class Session : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        [Expose] public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        [Expose] public Guid UserId { get; set; }
        public User? User { get; set; } = default;

        /// <summary>
        /// Hex SHA-256 of the current refresh token; rotated on every refresh.
        /// </summary>
        public string RefreshTokenHash { get; set; } = string.Empty;

        [Expose] public string? DeviceId { get; set; }

        /// <summary>
        /// Hashes of refresh tokens this session has already rotated away from.
        /// </summary>
        public ICollection<SessionAudit> Audits { get; set; } = [];

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
