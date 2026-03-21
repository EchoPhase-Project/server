// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.Security.Authorization.Requirements;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Handlers
{
    public class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
    {
        private readonly ScopesBitMask _mask;

        public ScopeAuthorizationHandler(ScopesBitMask mask)
        {
            _mask = mask;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ScopeRequirement requirement)
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
                if (deserialized.TryGetError(out var err))
                    context.Fail();
                return Task.CompletedTask;
            }

            var passed = requirement.RequireAll
                ? _mask.Has(bitmask!, requirement.Scopes)
                : requirement.Scopes.Any(s => _mask.Has(bitmask!, new[] { s }));

            if (!passed)
                context.Fail();
            else
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
