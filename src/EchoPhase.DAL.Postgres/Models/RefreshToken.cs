using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Abstractions;

namespace EchoPhase.DAL.Postgres.Models
{
    public class RefreshToken : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        public Guid Id
        {
            get; set;
        }

        public Guid UserId
        {
            get; set;
        }
        public User? User { get; set; } = default;

        public string RefreshValue { get; set; } = string.Empty;

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
