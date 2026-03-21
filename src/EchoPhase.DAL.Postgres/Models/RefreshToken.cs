// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    public class RefreshToken : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        [Expose] public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        [Expose] public Guid UserId { get; set; }
        public User? User { get; set; } = default;
        public string RefreshValue { get; set; } = string.Empty;
        [Expose] public string? DeviceId { get; set; }
        public ICollection<RefreshTokenAudit> Audits { get; set; } = [];
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [ConcurrencyCheck] public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        [Expose] public ICollection<RefreshTokenScope> Scopes { get; set; } = [];
        [Expose] public ICollection<RefreshTokenIntent> Intents { get; set; } = [];
        [Expose] public ICollection<RefreshTokenPermissionEntry> Permissions { get; set; } = [];
    }

    public abstract class RefreshTokenClaim : IIdentifiable
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        public Guid RefreshTokenId { get; set; }
        public RefreshToken RefreshToken { get; set; } = default!;
    }

    public class RefreshTokenScope : RefreshTokenClaim
    {
        [Expose] public string Value { get; set; } = string.Empty;
    }

    public class RefreshTokenIntent : RefreshTokenClaim
    {
        [Expose] public string Value { get; set; } = string.Empty;
    }

    public class RefreshTokenPermissionEntry : RefreshTokenClaim
    {
        [Expose] public string Resource { get; set; } = string.Empty;
        [Expose] public string Permission { get; set; } = string.Empty;
    }
}
