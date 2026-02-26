using Microsoft.AspNetCore.Identity;

namespace EchoPhase.DAL.Postgres.Models
{
    public class UserRole : IdentityRole<Guid>, ITrackingEntity
    {
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
