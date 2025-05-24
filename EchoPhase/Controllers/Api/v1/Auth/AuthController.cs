using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using EchoPhase.Clients;
using EchoPhase.Dtos;
using EchoPhase.Interfaces;

namespace EchoPhase.Controllers
{
	[ApiController]
	[AllowAnonymous]
	[Route("api/auth")]
	public class AuthController : ControllerBase
	{
		private readonly IAuthService _authService;
		private readonly IAntiforgeryService _antiforgeryService;

		public AuthController(IAuthService authService, IAntiforgeryService antiforgeryService)
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
		public async Task<IActionResult> Register([FromBody] RegisterDto dto)
		{
			//if (!await _antiforgeryService.ValidateAntiforgeryTokenAsync())
			//	return BadRequest("Invalid CSRF token");

			var result = await _authService.CreateUserAsync(dto.Name, dto.Username, dto.Password);
			return result.Succeeded ? Ok() : BadRequest(result.Errors);
		}

		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginDto dto)
		{
			//if (!await _antiforgeryService.ValidateAntiforgeryTokenAsync())
			//	return BadRequest("Invalid CSRF token");

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
