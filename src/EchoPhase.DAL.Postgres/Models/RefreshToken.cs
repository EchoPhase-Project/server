using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;

namespace EchoPhase.DAL.Postgres.Models
{
    public class RefreshToken : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        [Expose]
        public Guid Id
        {
            get; set;
        }

        [Expose]
        public Guid UserId
        {
            get; set;
        }
        public User? User { get; set; } = default;

        public string RefreshValue { get; set; } = string.Empty;

        [Expose]
        public string? DeviceId
        {
            get; set;
        }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ConcurrencyCheck]
        public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    }
}
