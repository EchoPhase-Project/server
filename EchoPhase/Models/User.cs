using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using EchoPhase.Interfaces;

namespace EchoPhase.Models
{
	[Comment("Authorised User Model")]
    public class User : IdentityUser<Guid>, ITrackingEntity, IDisposable
    {
		public string Name { get; set; }

		public string? ProfileImageName { get; set; } = default;

		public ICollection<JwtToken> JwtTokens { get; set; } = new List<JwtToken>();

		public ICollection<WebHook> WebHooks { get; set; } = new List<WebHook>();
		
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public User(string name)
		{
			Name = name;
		}

		[NotMapped]
		private bool _disposed = false;

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
