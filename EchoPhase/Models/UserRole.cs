using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Models
{
    public class UserRole : IdentityRole<Guid>
    {
        public UserRole(string name) : base(name)
        {
            Id = Guid.NewGuid();
        }
    }
}
