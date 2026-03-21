using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    public class RefreshTokenAudit : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        [Expose] public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        [Expose] public Guid RefreshTokenId { get; set; }
        public RefreshToken? RefreshToken { get; set; }
        [Expose] public string Token { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [ConcurrencyCheck] public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
