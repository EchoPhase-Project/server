// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Identity;
using EchoPhase.Projection;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication;
using EchoPhase.Security.Authentication.Attributes;
using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.Types.Repository;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/sessions")]
    [RequireUser]
    public class SessionController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly IRefreshTokenProvider _refreshProvider;
        private readonly Projector _projector;

        public SessionController(
            IAuthenticationService authService,
            IRefreshTokenProvider refreshProvider,
            IUserService userService,
            Projector projector)
        {
            _authService = authService;
            _refreshProvider = refreshProvider;
            _projector = projector;
        }

        private User currentUser =>
            (User)HttpContext.Items["User"]!;

        [HttpGet]
        public async Task<IActionResult> GetSessions([FromQuery] CursorOptions? cursor)
        {
            var sessions = await _refreshProvider.GetSessionsAsync(currentUser.Id, cursor);
            var projected = _projector
                .For(sessions)
                .Build();

            return Ok(projected);
        }

        [HttpDelete("current")]
        [ValidateAntiForgery]
        public async Task<IActionResult> RevokeCurrentSession()
        {
            var result = await _authService.LogoutAsync(currentUser.Id);
            return result.Successful ? NoContent() : BadRequest(result.Error);
        }

        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        public async Task<IActionResult> RevokeSession([FromRoute] Guid id)
        {
            var result = await _authService.RevokeSessionAsync(currentUser.Id, id);
            return result.Successful ? NoContent() : BadRequest(result.Error);
        }

        [HttpDelete]
        [ValidateAntiForgery]
        public async Task<IActionResult> RevokeAllSessions()
        {
            var result = await _authService.LogoutAllAsync(currentUser.Id);
            return result.Successful ? NoContent() : BadRequest(result.Error);
        }
    }
}
