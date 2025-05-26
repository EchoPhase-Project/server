using System.ComponentModel.DataAnnotations;

using EchoPhase.Interfaces;

namespace EchoPhase.Models
{
	public class JwtToken : ITrackingEntity, IConcurrentEntity
	{
		public int Id { get; set; }

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

		private string _token = string.Empty;
		public string Token {
			get
			{
				return _token;
			}
			set 
			{ 
				if (string.IsNullOrWhiteSpace(value)) 
					throw new InvalidOperationException("Token cannot be empty.");

				_token = value;
			} 
		}
		public User? User { get; set; } = default;

		public DateTime ExpiryDate { get; set; } = DateTime.UtcNow.AddDays(1);

		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		[ConcurrencyCheck]
		public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
	}
}
