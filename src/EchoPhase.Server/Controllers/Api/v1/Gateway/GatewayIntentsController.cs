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
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult List()
        {
            var granted = _permissions.GetPermissions(User);
            return Ok(Intents.AsEnumerable().Order(StringComparer.Ordinal).Select(name =>
            {
                var requirement = IntentRequirements.Map.TryGetValue(name, out var r) ? r : default;
                return new
                {
                    name,
                    resource = requirement.Resource,
                    permission = requirement.Permission,
                    allowed = IntentRequirements.Disallowed([name], granted).Count == 0
                };
            }));
        }
    }
}
