// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.Security.Authorization.Requirements;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.BitMask;
using EchoPhase.Types.Result.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Handlers
{
    public class PermissionAuthorizationHandler
        : AuthorizationHandler<PermissionRequirement>
    {
        private readonly ResourcePermissionsBitMask _mask;

        public PermissionAuthorizationHandler(ResourcePermissionsBitMask mask)
        {
            _mask = mask;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var claim = context.User
                .FindFirstValue(_mask.ClaimName);

            if (claim is null)
            {
                context.Fail();
                return Task.CompletedTask;
            }

            var deserialized = _mask.Deserialize(claim);
            if (!deserialized.Successful || !deserialized.TryGetValue(out var bitmasks))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            _ = ResourcePermissionsBitMask.KeyMask
                .Add(BitMaskBase.Empty, requirement.Resources);
            _ = _mask
                .Add(BitMaskBase.Empty, requirement.Permissions);

            foreach (var (keyBits, valBits) in bitmasks)
            {
                if (!ResourcePermissionsBitMask.KeyMask.Has(keyBits, requirement.Resources))
                    continue;

                if (!_mask.Has(valBits, requirement.Permissions))
                    continue;

                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            context.Fail();
            return Task.CompletedTask;
        }
    }
}
