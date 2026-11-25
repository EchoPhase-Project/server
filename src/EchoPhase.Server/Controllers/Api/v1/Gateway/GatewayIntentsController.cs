// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Security.BitMasks.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Gateway
{
    /// <summary>What a gateway client can subscribe to.</summary>
    [Route("api/v1/gateway/intents")]
    public class GatewayIntentsController : ApiControllerBase
    {
        private readonly ResourcePermissionsBitMask _permissions;

        public GatewayIntentsController(ResourcePermissionsBitMask permissions)
        {
            _permissions = permissions;
        }

        /// <summary>
        /// Every intent, the permission it needs, and whether the caller's
        /// token has it (an intent without a requirement is never allowed).
        /// </summary>
        [HttpGet]
        [ProducesResponseType<GatewayIntentResponse[]>(StatusCodes.Status200OK)]
        public IActionResult List()
        {
            var granted = _permissions.GetPermissions(User);
            return Ok(Intents.AsEnumerable().Order(StringComparer.Ordinal).Select(name =>
            {
                bool known = IntentRequirements.Map.TryGetValue(name, out var requirement);
                return new GatewayIntentResponse(
                    name,
                    known ? requirement.Resource : null,
                    known ? requirement.Permission : null,
                    IntentRequirements.Disallowed([name], granted).Count == 0);
            }).ToList());
        }
    }

    /// <summary>One intent of the gateway, and whether the caller may subscribe to it.</summary>
    /// <param name="Name">The intent, as in the handshake.</param>
    /// <param name="Resource">The resource its permission is on; none: the intent is never allowed.</param>
    /// <param name="Permission">The action needed on that resource.</param>
    /// <param name="Allowed">Whether the caller's token has that permission.</param>
    public sealed record GatewayIntentResponse(string Name, string? Resource, string? Permission, bool Allowed);
}
