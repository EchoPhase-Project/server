using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;

namespace EchoPhase.DAL.Postgres.Models
{
    public class RefreshToken : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        [Expose] public Guid Id { get; set; }
        [Expose] public Guid UserId { get; set; }
        public User? User { get; set; } = default;
        public string RefreshValue { get; set; } = string.Empty;
        [Expose] public string? DeviceId { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [ConcurrencyCheck] public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
        [Expose] public ICollection<RefreshTokenScope>           Scopes      { get; set; } = [];
        [Expose] public ICollection<RefreshTokenIntent>          Intents     { get; set; } = [];
        [Expose] public ICollection<RefreshTokenPermissionEntry> Permissions { get; set; } = [];
    }

    public abstract class RefreshTokenClaim : IIdentifiable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
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
        [Expose] public string Resource   { get; set; } = string.Empty;
        [Expose] public string Permission { get; set; } = string.Empty;
    }
}
