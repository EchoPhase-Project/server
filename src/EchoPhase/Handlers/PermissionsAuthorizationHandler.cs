using System.Text.Json;
using EchoPhase.Extensions;
using EchoPhase.Types.Extensions;
using EchoPhase.Requirements;
using EchoPhase.Services.Bitmasks;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Handlers
{
    public class PermissionsAuthorizationHandler : AuthorizationHandler<PermissionsRequirement>
    {
        public PermissionsAuthorizationHandler()
        {
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionsRequirement requirement)
        {
            var claim = context.User.FindFirst(PermissionsBitMaskService.ClaimName);
            if (string.IsNullOrWhiteSpace(claim?.Value))
                return Task.CompletedTask;

            var claimValue = claim.Value;

            Dictionary<string, string[]>? parsedPermissions = null;
            try
            {
                parsedPermissions = JsonSerializer.Deserialize<Dictionary<string, string[]>>(claimValue);
            }
            catch { }

            if (parsedPermissions is not null)
            {
                foreach (var (resource, requiredPermissions) in requirement.Permissions)
                {
                    var key = resource.ToString();
                    if (!parsedPermissions.TryGetValue(key, out var userPermissions))
                        return Task.CompletedTask;

                    if (!requiredPermissions.All(rp => userPermissions.Contains(rp)))
                        return Task.CompletedTask;
                }

                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var deserialized = PermissionsBitMaskService.Deserialize(claimValue);
            deserialized.OnSuccess(userPermissions =>
            {
                foreach (var (resource, requiredBits) in requirement.PermissionsBitmasks)
                {
                    if (!userPermissions.TryGetValue(resource, out var userBits))
                        return;

                    if (!userBits.AllRequiredBitsSet(requiredBits))
                        return;
                }

                context.Succeed(requirement);
            });

            return Task.CompletedTask;
        }
    }
}
