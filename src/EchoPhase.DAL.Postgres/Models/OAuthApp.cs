// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A user-owned ("bring your own") OAuth application. The platform's own
    /// applications live in configuration, not in this table.
    /// </summary>
    public class OAuthApp : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        [Expose] public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        [Expose] public Guid UserId { get; set; }
        public User? User { get; set; }

        [Expose] public string Provider { get; set; } = string.Empty;
        [Expose] public string Name { get; set; } = string.Empty;
        [Expose] public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Encrypted client secret; <c>null</c> for public clients.
        /// </summary>
        public byte[]? ClientSecret { get; set; }

        public ICollection<Connection> Connections { get; set; } = new List<Connection>();

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
