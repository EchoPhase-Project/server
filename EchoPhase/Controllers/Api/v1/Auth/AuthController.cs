using System.ComponentModel.DataAnnotations;
using EchoPhase.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IAntiforgeryService _antiforgeryService;

        public AuthController(
            IAuthService authService,
            IAntiforgeryService antiforgeryService
        )
        {
            _authService = authService;
            _antiforgeryService = antiforgeryService;
        }

        [HttpGet("csrf")]
        public IActionResult GetToken()
        {
            if (_antiforgeryService.SetAntiforgeryToken())
                return Ok(new { message = "Token set" });

            return BadRequest();
        }

        [HttpPost("register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var result = await _authService.CreateUserAsync(dto.Name, dto.Username, dto.Password);
            return result.Succeeded ? Ok() : BadRequest(result.Errors);
        }

        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _authService.AuthenticateAsync(dto.Username, dto.Password);
            return result.Succeeded ? Ok() : Unauthorized();
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
