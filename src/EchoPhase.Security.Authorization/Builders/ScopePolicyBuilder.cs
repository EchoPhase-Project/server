using Microsoft.AspNetCore.Authorization;
using EchoPhase.Security.Authorization.Requirements;
using EchoPhase.Security.Authorization.Attributes;

namespace EchoPhase.Security.Authorization.Builders
{
    [PolicyPrefix("scope:")]
    public class ScopePolicyBuilder : IPolicyBuilder
    {
        public AuthorizationPolicy? Build(string body)
        {
            var parts = body.Split(':', 2);
            bool requireAll = parts.Length != 2 || parts[0] != "any";
            var scopes = (parts.Length == 2 ? parts[1] : parts[0])
                .Split(',', StringSplitOptions.RemoveEmptyEntries);

            if (scopes.Length == 0) return null;

            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ScopeRequirement(scopes, requireAll))
                .Build();
        }
    }
}
