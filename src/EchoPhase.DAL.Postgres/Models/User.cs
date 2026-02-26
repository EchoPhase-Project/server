using System.ComponentModel.DataAnnotations.Schema;
using EchoPhase.Projection.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Models
{
    [Comment("Authorised User Model")]
    public class User : IdentityUser<Guid>, ITrackingEntity, IDisposable
    {
        [Expose]
        public string Name
        {
            get; set;
        }

        public string? ProfileImageName { get; set; } = default;

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<DiscordToken> DiscordTokens { get; set; } = new List<DiscordToken>();

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
