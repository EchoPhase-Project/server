using EchoPhase.Identity;
using EchoPhase.Security.Authentication.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EchoPhase.Controllers.Api.v1.Dto.Auth;
using EchoPhase.Types.Repository;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/auth/refresh")]
    public class RefreshTokenController : ControllerBase
    {
        private readonly IRefreshTokenProvider _refreshTokenProvider;
        private readonly IUserService _userService;

        public RefreshTokenController(
            IRefreshTokenProvider refreshTokenProvider,
            IUserService userService
            )
        {
            _refreshTokenProvider = refreshTokenProvider;
            _userService = userService;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            var tokenPair = await _refreshTokenProvider.RefreshAsync(request.DeviceId, request.RefreshToken);
            return Ok(tokenPair);
        }

        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke([FromBody] RefreshRequest request)
        {
            var user = await _userService.GetAsync(User);
            if (user is null || user.Id == Guid.Empty)
                return Unauthorized();

            await _refreshTokenProvider.RevokeAsync(user.Id, request.DeviceId, request.RefreshToken);
            return NoContent();
        }

        [HttpDelete("all")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeAll()
        {
            var user = await _userService.GetAsync(User);
            if (user is null || user.Id == Guid.Empty)
                return Unauthorized();

            await _refreshTokenProvider.RevokeAllAsync(user.Id);
            return NoContent();
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions([FromQuery] string? after, [FromQuery] int limit = 20)
        {
            var user = await _userService.GetAsync(User);
            if (user is null || user.Id == Guid.Empty)
                return Unauthorized();

            var sessions = await _refreshTokenProvider.GetSessionsAsync(user.Id, new CursorOptions
            {
                After = after,
                Limit = limit
            });
            return Ok(sessions);
        }
    }
}
