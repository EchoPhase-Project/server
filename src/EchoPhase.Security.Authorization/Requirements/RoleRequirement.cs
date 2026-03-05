using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Requirements
{
    public class RoleRequirement : IAuthorizationRequirement
    {
        public string[] Roles { get; }
        public bool RequireAll { get; }

        public RoleRequirement(string[] roles, bool requireAll = false)
        {
            Roles      = roles;
            RequireAll = requireAll;
        }
    }
}
