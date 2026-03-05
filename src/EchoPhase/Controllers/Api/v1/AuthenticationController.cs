using EchoPhase.Controllers.Api.v1.Dto.Auth;
using EchoPhase.Identity;
using EchoPhase.Projection;
using EchoPhase.Security.Authentication;
using EchoPhase.Security.Antiforgery.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly IUserService           _userService;
        private readonly Projector              _projector;

        public AuthenticationController(
            IAuthenticationService authService,
            IUserService           userService,
            Projector              projector)
        {
            _authService        = authService;
            _userService        = userService;
            _projector          = projector;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var registration = await _userService.CreateUserAsync(dto.Name, dto.Username, dto.Password);
            if (!registration.Succeeded)
                return BadRequest(registration.Errors);

            var auth = await _authService.LoginAsync(dto.Username, dto.Password);
            if (!auth.Successful)
                return Unauthorized(auth.Error);

            return NoContent();
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _authService.LoginAsync(dto.Username, dto.Password);
            if (!result.Successful)
                return Unauthorized(result.Error);

            return NoContent();
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var projected = _projector
                .For(user)
                .Include(u => u.UserName, u => u.Id)
                .Build();

            return Ok(projected);
        }

        [Authorize]
        [HttpPost("logout")]
        [ValidateAntiForgery]
        public async Task<IActionResult> Logout()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _authService.LogoutAsync(user.Id);
            if (!result.Successful)
                return BadRequest(result.Error);

            return NoContent();
        }

        /*
        [HttpPost("logout-all")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutAll()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _authService.LogoutAllAsync(user.Id);
            if (!result.Successful)
                return BadRequest(result.Error);

            return NoContent();
        }
        */
    }
}
