// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Controllers.Api.v1.Common;
using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1.Sessions
{
    [Route("api/v1/sessions")]
    [RequirePrincipal(PrincipalKind.Session)]
    public class SessionsController : ApiControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly ISessionProvider _sessionProvider;

        public SessionsController(
            IAuthenticationService authService,
            ISessionProvider sessionProvider)
        {
            _authService = authService;
            _sessionProvider = sessionProvider;
        }

        [HttpGet]
        [RequirePermission(Resources.Sessions, Permissions.Read)]
        public async Task<ActionResult<CursorPageResponse<SessionResponse>>> GetSessions(
            [FromQuery] CursorOptions? cursor, CancellationToken ct = default)
        {
            var page = await _sessionProvider.GetSessionsAsync(User.GetUserId(), cursor, ct);
            return new CursorPageResponse<SessionResponse>(
                page.Data.Select(SessionResponse.From).ToList(), page.NextCursor);
        }

        /// <summary>Signs out this device: revokes its session and clears the token cookies.</summary>
        [HttpDelete("current")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Sessions, Permissions.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RevokeCurrentSession(CancellationToken ct = default)
        {
            var result = await _authService.LogoutAsync(User.GetUserId(), ct);
            return result.Successful ? NoContent() : BadRequest(ErrorBody.From(result.Error));
        }

        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Sessions, Permissions.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RevokeSession([FromRoute] Guid id, CancellationToken ct = default)
        {
            var result = await _authService.RevokeSessionAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : BadRequest(ErrorBody.From(result.Error));
        }

        [HttpDelete]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Sessions, Permissions.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RevokeAllSessions(CancellationToken ct = default)
        {
            var result = await _authService.LogoutAllAsync(User.GetUserId(), ct);
            return result.Successful ? NoContent() : BadRequest(ErrorBody.From(result.Error));
        }
    }
}
