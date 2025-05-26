using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;

using EchoPhase.Interfaces;

namespace EchoPhase.Models
{
	public class DiscordToken : IConcurrentEntity, ITrackingEntity
	{
		public Guid Id { get; set; }
		private string _token = string.Empty;
		public string Token { 
			get
			{
				return _token;
			} 
			set
			{
				if (!TokenRegex.IsMatch(value))
					throw new InvalidOperationException("Invalid discord token format.");
				_token = value;
			}
		}
		public string Name { get; set; } = string.Empty;
		
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
		public User? User { get; set; }

		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		[ConcurrencyCheck]
		public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

		private static readonly Regex TokenRegex = new Regex(@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$", RegexOptions.Compiled);
	}
}
