// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Handlers
{
    public class PrincipalAuthorizationHandler : AuthorizationHandler<PrincipalRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PrincipalRequirement requirement)
        {
            if (PrincipalKinds.Of(context.User) == requirement.Kind)
                context.Succeed(requirement);
            else
                context.Fail();

            return Task.CompletedTask;
        }
    }
}
