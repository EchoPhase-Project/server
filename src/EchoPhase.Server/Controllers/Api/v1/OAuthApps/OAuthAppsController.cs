// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections.Services;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.OAuthApps
{
    /// <summary>
    /// User-owned OAuth applications ("bring your own app"). Their redirect URI
    /// must match the platform's configured callback URL.
    /// </summary>
    [Route("api/v1/oauth-apps")]
    [RequirePrincipal(PrincipalKind.Session)]
    public class OAuthAppsController : ApiControllerBase
    {
        private readonly IOAuthAppService _apps;

        public OAuthAppsController(IOAuthAppService apps)
        {
            _apps = apps;
        }

        [HttpGet]
        [RequirePermission(Resources.OAuthApps, Permissions.Read)]
        public async Task<IActionResult> List(CancellationToken ct = default)
            => Ok(await _apps.ListAsync(User.GetUserId(), ct));

        [HttpPost]
        [ValidateAntiForgery]
        [RequirePermission(Resources.OAuthApps, Permissions.Create)]
        public async Task<IActionResult> Save([FromBody] SaveOAuthAppRequest dto, CancellationToken ct = default)
        {
            var result = await _apps.SaveAsync(
                User.GetUserId(), dto.Provider, dto.Name, dto.ClientId, dto.ClientSecret, ct);

            return result.Successful ? Ok(result.Value) : Failure(result.Error);
        }

        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.OAuthApps, Permissions.Delete)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var result = await _apps.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }
    }
}
