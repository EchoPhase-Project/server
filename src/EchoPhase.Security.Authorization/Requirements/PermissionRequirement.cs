using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Requirements
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string[] Resources   { get; }
        public string[] Permissions { get; }

        public PermissionRequirement(string[] resources, string[] permissions)
        {
            Resources   = resources;
            Permissions = permissions;
        }
    }
}
