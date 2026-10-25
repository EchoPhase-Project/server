// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A refresh token that was rotated away from. Presenting it again means
    /// either a parallel request (within the grace period) or token theft.
    /// </summary>
    public class SessionAudit : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        public Guid SessionId { get; set; }
        public Session? Session { get; set; }

        /// <summary>
        /// Hex SHA-256 of the rotated refresh token.
        /// </summary>
        public string TokenHash { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
