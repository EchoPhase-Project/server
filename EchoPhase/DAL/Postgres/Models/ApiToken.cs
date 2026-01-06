using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using EchoPhase.Interfaces;

namespace EchoPhase.DAL.Postgres.Models
{
    public class DiscordToken : IConcurrentEntity, ITrackingEntity
    {
        public Guid Id
        {
            get; set;
        }
        public string Token { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public Guid UserId
        {
            get; set;
        }
        public User? User
        {
            get; set;
        }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ConcurrencyCheck]
        public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

        private static readonly Regex TokenRegex = new Regex(@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    }
}
