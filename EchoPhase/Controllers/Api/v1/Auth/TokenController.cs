using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using ParkSquare.AspNetCore.Sitemap;

using EchoPhase.DAL.Postgres;
using EchoPhase.Interfaces;
using EchoPhase.Models;
using EchoPhase.Services.Security;

namespace PointsApp.Controllers
{
	[Route("/api/v1/tokens")]
	[ApiController]
	[SitemapExclude]
	public class TokenAPIController : ControllerBase
	{
        private readonly PostgresContext _context;
		private readonly IUserService _userService;
		private readonly IAuthService _authService;
		private readonly RoleService _roleService;

		public TokenAPIController(
			PostgresContext context, 
			IUserService userService, 
			IAuthService authService, 
			RoleService roleService
		)
		{
            _context = context;
			_userService = userService;
			_authService = authService;
			_roleService = roleService;
		}

		[HttpGet]
		[Route("{guid:guid}", Name = "GenerateTokenGuid")]
		[Authorize(Policy = "APIAccess")]
		public async Task<IActionResult> TokenGeneration(Guid guid)
		{
			User user = await _userService.GetUserAsync(guid);
			if (user == null)
                return NotFound();

			var token = await _authService.GenerateTokenAsync(user);

			return Ok(new { token = token });
		}

		[HttpGet]
		[Route("{username}", Name = "GenerateTokenUserName")]
		[Authorize(Policy = "APIAccess")]
		public async Task<IActionResult> TokenGeneration(string username)
		{
			User user = await _userService.GetUserAsync(username);
			if (user == null)
                return NotFound();

			var token = await _authService.GenerateTokenAsync(user);

			return Ok(new { token = token });
		}

		[HttpGet]
		[Route("", Name = "GenerateTokenSelf")]
		[Authorize(Policy = "APIAccess")]
		public async Task<IActionResult> TokenGeneration()
		{
			User user = await _userService.GetUserAsync(User);
			if (user == null)
                return NotFound();

			Console.WriteLine(User.Identity?.AuthenticationType);

			var token = await _authService.GenerateTokenAsync(user);

			return Ok(new { token = token });
		}

		[HttpPost]
		[Route("", Name = "ValidateToken")]
		[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "APIAccess")]
		public async Task<IActionResult> TokenValidation()
		{
			User user = await _userService.GetUserAsync(User);
			if (user == null)
				return Unauthorized(new { Error = "Invalid token or token expired."});
			
			List<string> roles = await _roleService.GetRolesAsync(user);

			return Ok(new { Id = user.Id, Username = user.UserName, Roles = roles });
		}

		[HttpDelete]
		[Route("", Name = "InvalidateToken")]
		[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "APIAccess")]
		public IActionResult TokenInvalidation([FromBody] TokenInvalidationDto dto)
		{
			if (string.IsNullOrEmpty(dto.Token))
				return BadRequest("Invalid parameters or arguments.");

			_authService.RevokeToken(dto.Token);
			
			return Ok("Token invalidated successfully.");
		}

		[HttpPut]
		[Route("", Name = "ExtendToken")]
		[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "APIAccess")]
		public IActionResult TokenExtension([FromBody] TokenExtensionDto dto)
		{
			if (string.IsNullOrEmpty(dto.Token))
				return BadRequest("Invalid parameters or arguments.");

			_authService.ExtendToken(dto.Token);
			
			return Ok("Token extended successfully.");
		}
	}
}

namespace EchoPhase.Models
{
		public class TokenInvalidationDto
		{
			public string? Token { get; set; }
		}

		public class TokenExtensionDto
		{
			public string? Token { get; set; }
		}
}

