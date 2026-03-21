using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    public class ExternalToken : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        [Expose] public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        [Expose] public Guid UserId { get; set; }
        public User? User { get; set; }
        [Expose] public string ProviderName { get; set; } = string.Empty;
        [Expose] public string TokenName { get; set; } = string.Empty;
        public byte[] Value { get; set; } = [];
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [ConcurrencyCheck] public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
    }
}
