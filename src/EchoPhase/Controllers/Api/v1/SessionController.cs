using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Identity;
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
        private readonly IUserService _userService;

        public SessionController(
            IAuthenticationService authService,
            IRefreshTokenProvider refreshProvider,
            IUserService userService)
        {
            _authService = authService;
            _refreshProvider = refreshProvider;
            _userService = userService;
        }

        private User currentUser =>
            (User)HttpContext.Items["User"]!;

        [HttpGet]
        public async Task<IActionResult> GetSessions([FromQuery] CursorOptions? cursor)
        {
            var sessions = await _refreshProvider.GetSessionsAsync(currentUser.Id, cursor);
            return Ok(sessions);
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
