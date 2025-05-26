using System.ComponentModel.DataAnnotations;

using EchoPhase.Enums;
using EchoPhase.Attributes;
using EchoPhase.Interfaces;

namespace EchoPhase.Models
{
	public class WebHook : ITrackingEntity, IConcurrentEntity
	{
		public Guid Id { get; set; }

		public WebHookStatus Status { get; set; } = WebHookStatus.Enabled;

		private Guid _userId;
		public Guid UserId { 
			get
			{
				return _userId;
			}
			set 
			{ 
				if (value == Guid.Empty) 
					throw new InvalidOperationException("Guid cannot be empty.");

				_userId = value;
			} 
		}

		private string _url = string.Empty;
		public string Url {
			get
			{
				return _url;
			}
			set 
			{ 
				if (string.IsNullOrWhiteSpace(value) || !IsValidUrl(value)) 
					throw new InvalidOperationException("Invalid URL.");

				_url = value;
			} 
		}

		public User? User { get; set; }

		public string Name { get; set; } = string.Empty;

		private long _intents = 0;
		public long Intents {
			get
			{
				return _intents;
			}
			set 
			{ 
				if (value == 0) 
					throw new InvalidOperationException("Intents cannot be 0");

				_intents = value;
			} 
		}

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		[ConcurrencyCheck]
		public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

		private bool IsValidUrl(string url)
		{
			return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
				   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
		}
	}
}
