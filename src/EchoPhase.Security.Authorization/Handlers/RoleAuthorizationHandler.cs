// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.Security.Authorization.Requirements;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Handlers
{
    public class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
    {
        private readonly RolesBitMask _mask;

        public RoleAuthorizationHandler(RolesBitMask mask)
        {
            _mask = mask;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            RoleRequirement requirement)
        {
            var claim = context.User.FindFirstValue(_mask.ClaimName);

            if (claim is null)
            {
                context.Fail();
                return Task.CompletedTask;
            }

            var deserialized = _mask.Deserialize(claim);
            if (!deserialized.Successful || !deserialized.TryGetValue(out var bitmask))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            if (requirement.RequireAll)
            {
                if (!_mask.Has(bitmask!, requirement.Roles))
                { context.Fail(); return Task.CompletedTask; }
            }
            else
            {
                if (!requirement.Roles.Any(role => _mask.Has(bitmask!, new[] { role })))
                { context.Fail(); return Task.CompletedTask; }
            }

            context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}
