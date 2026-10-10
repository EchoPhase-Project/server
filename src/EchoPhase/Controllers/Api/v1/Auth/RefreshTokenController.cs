// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.RateLimiting;
using EchoPhase.Security.Authentication.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EchoPhase.Controllers.Api.v1.Auth
{
    /// <summary>
    /// Refresh for interactive sessions. Bots and servers use API clients
    /// (<c>POST /api/v1/auth/token</c>) instead.
    /// </summary>
    [Route("api/v1/auth/refresh")]
    public class RefreshTokenController : ApiControllerBase
    {
        private readonly RefreshSignInManager _refreshSignInManager;

        public RefreshTokenController(RefreshSignInManager refreshSignInManager)
        {
            _refreshSignInManager = refreshSignInManager;
        }

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct = default)
        {
            try
            {
                // RefreshToken is null when a parallel call already rotated the
                // token; keep using the one that call returned.
                var tokenPair = await _refreshSignInManager.RefreshAsync(request.RefreshToken, ct);
                return Ok(tokenPair);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}
