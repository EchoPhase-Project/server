using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Requirements
{
    public class ScopeRequirement : IAuthorizationRequirement
    {
        public string[] Scopes { get; }
        public bool RequireAll { get; }

        public ScopeRequirement(string[] scopes, bool requireAll = true)
        {
            Scopes     = scopes;
            RequireAll = requireAll;
        }
    }
}
