// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Controllers.Api.v1.Dto;
using EchoPhase.Controllers.Api.v1.Dto.Auth;
using EchoPhase.Identity;
using EchoPhase.Projection;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.Types.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/auth/refresh")]
    public class RefreshTokenController : ControllerBase
    {
        private readonly IRefreshTokenProvider _refreshTokenProvider;
        private readonly IUserService _userService;
        private readonly Projector _projector;

        public RefreshTokenController(
            IRefreshTokenProvider refreshTokenProvider,
            IUserService userService,
            Projector projector
            )
        {
            _refreshTokenProvider = refreshTokenProvider;
            _userService = userService;
            _projector = projector;
        }

        [HttpPost("new")]
        [ValidateAntiForgery]
        public async Task<IActionResult> Create([FromBody] CreateDto dto)
        {
            var user = await _userService.GetAsync(User);
            if (user is null || user.Id == Guid.Empty)
                return Unauthorized();

            var tokenPair = dto.Claims is null
                ? await _refreshTokenProvider.CreateAsync(user, dto.DeviceId)
                : await _refreshTokenProvider.CreateAsync(user, dto.DeviceId, new ClaimsEnrichmentContext
                {
                    User = user,
                    RequestedScopes = dto.Claims.Scopes ?? [],
                    RequestedIntents = dto.Claims.Intents ?? [],
                    RequestedPermissions = dto.Claims.Permissions ?? new Dictionary<string, string[]>()
                });

            return Ok(tokenPair);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            var tokenPair = await _refreshTokenProvider.RefreshAsync(request.RefreshId, request.RefreshToken);
            return Ok(tokenPair);
        }

        [HttpDelete]
        [ValidateAntiForgery]
        public async Task<IActionResult> Revoke([FromBody] DeleteRefreshRequest request)
        {
            var user = await _userService.GetAsync(User);
            if (user is null || user.Id == Guid.Empty)
                return Unauthorized();

            await _refreshTokenProvider.RevokeAsync(user.Id, request.RefreshId);
            await _userService.UpdateSecurityStampAsync(user);
            return NoContent();
        }

        [HttpDelete("all")]
        [ValidateAntiForgery]
        public async Task<IActionResult> RevokeAll()
        {
            var userId = User.GetUserId();
            await _refreshTokenProvider.RevokeAllAsync(userId);
            return NoContent();
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions([FromQuery] CursorDto dto)
        {
            var userId = User.GetUserId();
            var sessions = await _refreshTokenProvider.GetSessionsAsync(userId, new CursorOptions
            {
                After = dto.After,
                Limit = dto.Limit
            });

            var projected = _projector
                .For(sessions)
                .Build();

            return Ok(projected);
        }
    }
}
