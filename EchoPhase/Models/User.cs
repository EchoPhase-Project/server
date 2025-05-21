using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using EchoPhase.Interfaces;

namespace EchoPhase.Models
{
	[Table("Users")]
	[Comment("Authorised User Model")]
    public class User : IdentityUser<Guid>, ITrackingEntity, IDisposable
    {
		[Required]
		[MaxLength(36)]
		[Column(Order = 1)]
		public string? Name { get; set; }

		[MaxLength(128)]
		public string? ProfileImageName { get; set; } = default;

		public ICollection<JwtToken> JwtTokens { get; set; } = new List<JwtToken>();

		public ICollection<WebHook> WebHooks { get; set; } = new List<WebHook>();
		
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		[NotMapped]
		private bool _disposed = false;

		public User()
		{}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
				_disposed = true;
		}

		~User() =>
			Dispose(false);
	}
}
