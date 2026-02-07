using System.Security.Claims;
using EchoPhase.Extensions;
using EchoPhase.Types.Extensions;
using EchoPhase.Requirements;
using EchoPhase.Services.Bitmasks;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Handlers
{
    public class RolesAuthorizationHandler : AuthorizationHandler<RolesRequirement>
    {
        public RolesAuthorizationHandler()
        {
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RolesRequirement requirement)
        {
            var claims = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

            if (claims.Length > 0 && requirement.Roles?.Length > 0)
            {
                if (claims.Intersect(requirement.Roles).Any())
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }

            var claim = context.User.FindFirst(RolesBitMaskService.ClaimName);

            if (!string.IsNullOrWhiteSpace(claim?.Value) && requirement.RolesBitmasks is { Count: > 0 })
            {
                var parseResult = RolesBitMaskService.Deserialize(claim.Value);

                var deserialized = RolesBitMaskService.Deserialize(claim.Value);
                if (deserialized.TryGetValue(out var userRoles))
                {
                    if (userRoles.AnyRequiredBitsSet(requirement.RolesBitmasks))
                        context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }

            return Task.CompletedTask;
        }
    }
}
