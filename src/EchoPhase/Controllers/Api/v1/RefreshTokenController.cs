using EchoPhase.Controllers.Api.v1.Dto.Auth;
using EchoPhase.Identity;
using EchoPhase.Projection;
using EchoPhase.Security.Authentication.Jwt;
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateDto dto)
        {
            var user = await _userService.GetAsync(User);
            if (user is null || user.Id == Guid.Empty)
                return Unauthorized();

            var tokenPair = await _refreshTokenProvider.CreateAsync(user, dto.DeviceId);
            return Ok(tokenPair);
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

            return Ok(_projector.Project(sessions));
        }
    }
}
