using EchoPhase.Controllers.Api.v1.Dto.Auth;
using EchoPhase.Identity;
using EchoPhase.Projection;
using EchoPhase.Security.Antiforgery;
using EchoPhase.Security.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IAntiforgeryService _antiforgeryService;
        private readonly IUserService _userService;
        private readonly Projector _projector;

        public AuthController(
            IAuthService authService,
            IAntiforgeryService antiforgeryService,
            IUserService userService,
            Projector projector)
        {
            _authService = authService;
            _antiforgeryService = antiforgeryService;
            _userService = userService;
            _projector = projector;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var registrationResult = await _authService.CreateUserAsync(dto.Name, dto.Username, dto.Password);
            if (!registrationResult.Succeeded)
                return BadRequest(registrationResult.Errors);

            var signInResult = await _authService.AuthenticateAsync(dto.Username, dto.Password);
            if (!signInResult.Succeeded)
                return Unauthorized();

            return NoContent();
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _authService.AuthenticateAsync(dto.Username, dto.Password);
            if (!result.Succeeded)
                return Unauthorized();

            return NoContent();
        }

        [HttpGet("antiforgery")]
        public IActionResult GetAntiforgeryToken()
        {
            _antiforgeryService.Set();
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

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync();
            _antiforgeryService.Remove();
            return NoContent();
        }
    }
}
