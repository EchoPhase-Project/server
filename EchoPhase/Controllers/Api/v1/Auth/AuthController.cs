using System.ComponentModel.DataAnnotations;
using EchoPhase.Interfaces;
using EchoPhase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EchoPhase.Helpers;

namespace EchoPhase.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IAntiforgeryService _antiforgeryService;
        private readonly IUserService _userService;
        private readonly ProjectionHelper _projection;
        private readonly TaskSchedulerService _scheduledTaskService;

        public AuthController(
            IAuthService authService,
            IAntiforgeryService antiforgeryService,
            IUserService userService,
            ProjectionHelper projection,
            TaskSchedulerService scheduledTaskService
        )
        {
            _authService = authService;
            _antiforgeryService = antiforgeryService;
            _userService = userService;
            _projection = projection;
            _scheduledTaskService = scheduledTaskService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var result = await _authService.CreateUserAsync(dto.Name, dto.Username, dto.Password);
            return result.Succeeded ? Ok() : BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _authService.AuthenticateAsync(dto.Username, dto.Password);
            if (!result.Succeeded)
                return Unauthorized();

            _antiforgeryService.Set();
            return Ok();
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var user = await _userService.GetAsync(User);
            if (user == null)
                return Unauthorized();


            return Ok(_projection.Project(user, u => u.UserName, u => u.Id));
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync();
            return Ok();
        }
    }

    public class LoginDto
    {
        [Required]
        [MinLength(3)]
        public string Username { get; set; } = default!;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = default!;
    }

    public class RegisterDto
    {
        [Required]
        [MinLength(2)]
        public string Name { get; set; } = default!;

        [Required]
        [MinLength(3)]
        public string Username { get; set; } = default!;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = default!;
    }
}
