using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class TokenModel
    {
        [Expose] public Guid Id { get; set; } = Guid.NewGuid();
        [Expose] public string DeviceId { get; set; } = "Linux";
        public string RefreshValue { get; set; } = "secret-token";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
