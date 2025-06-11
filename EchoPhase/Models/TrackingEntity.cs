using EchoPhase.Interfaces;

namespace EchoPhase.Models
{
    public abstract class TrackingEntity : ITrackingEntity
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
