// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections.Models;
using EchoPhase.Connections.Services;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
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
        [ProducesResponseType<OAuthAppResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List(CancellationToken ct = default)
            => Ok((await _apps.ListAsync(User.GetUserId(), ct)).Select(OAuthAppResponse.From).ToList());

        [HttpPost]
        [ValidateAntiForgery]
        [RequirePermission(Resources.OAuthApps, Permissions.Create)]
        [ProducesResponseType<OAuthAppResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Save([FromBody] SaveOAuthAppRequest dto, CancellationToken ct = default)
        {
            var result = await _apps.SaveAsync(
                User.GetUserId(), dto.Provider, dto.Name, dto.ClientId, dto.ClientSecret, ct);

            return result.Successful ? Ok(OAuthAppResponse.From(result.Value)) : Failure(result.Error);
        }

        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.OAuthApps, Permissions.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var result = await _apps.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }
    }

    /// <summary>An OAuth app of the user's own; its secret is never returned.</summary>
    /// <param name="Id">The app's id.</param>
    /// <param name="Provider">The service it is registered at.</param>
    /// <param name="Name">The owner's name for it.</param>
    /// <param name="ClientId">The client id at the provider (public).</param>
    /// <param name="HasSecret">Whether a client secret is stored.</param>
    /// <param name="CreatedAt">When it was added.</param>
    public sealed record OAuthAppResponse(Guid Id, string Provider, string Name, string ClientId, bool HasSecret, DateTime CreatedAt)
    {
        public static OAuthAppResponse From(OAuthAppInfo a) => new(a.Id, a.Provider, a.Name, a.ClientId, a.HasSecret, a.CreatedAt);
    }
}
