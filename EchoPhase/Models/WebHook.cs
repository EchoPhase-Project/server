using EchoPhase.Enums;
using EchoPhase.Processors.Enums;
using EchoPhase.Attributes;
using EchoPhase.Interfaces;

namespace EchoPhase.Models
{
	public class WebHook : ITrackingEntity
	{
		public Guid Id { get; set; }

		[AlwaysMerge]
		public required string Url { get; set; }

		public WebHookStatus Status { get; set; } = WebHookStatus.Enabled;

		public required Guid UserId { get; set; }
		public User? User { get; set; } = default!;

		[AlwaysMerge]
		public long Intents { get; set; } = (long)IntentsFlags.None;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
