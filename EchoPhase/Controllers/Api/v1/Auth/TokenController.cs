using EchoPhase.Interfaces;
using EchoPhase.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkSquare.AspNetCore.Sitemap;

namespace PointsApp.Controllers
{
    [Route("/api/v1/tokens")]
    [ApiController]
    [SitemapExclude]
    public class TokenAPIController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IJwtTokenService _jwtService;

        public TokenAPIController(
            IUserService userService,
            IJwtTokenService jwtService
        )
        {
            _userService = userService;
            _jwtService = jwtService;
        }

        [HttpGet]
        [Route("{guid:guid}", Name = "GenerateTokenGuid")]
        [Authorize(Policy = "ApiAccess")]
        public async Task<IActionResult> TokenGeneration(Guid guid)
        {
            var users = _userService.Get(x =>
            {
                x.Ids = new HashSet<Guid>() { guid };
            }).ToHashSet();

            if (users is { Count: 0 })
                return NotFound();

            IDictionary<Guid, string> dict = new Dictionary<Guid, string>();
            foreach (var user in users)
                dict[user.Id] = await _jwtService.GenerateTokenAsync(user);

            return Ok(dict);
        }

        [HttpGet]
        [Route("{username}", Name = "GenerateTokenUserName")]
        [Authorize(Policy = "ApiAccess")]
        public async Task<IActionResult> TokenGeneration(string username)
        {
            var users = _userService.Get(x =>
            {
                x.UserNames = new HashSet<string>() { username };
            }).ToHashSet();

            if (users is { Count: 0 })
                return NotFound();

            IDictionary<Guid, string> dict = new Dictionary<Guid, string>();
            foreach (var user in users)
                dict[user.Id] = await _jwtService.GenerateTokenAsync(user);

            return Ok(dict);
        }

        [HttpGet]
        [Route("", Name = "GenerateTokenSelf")]
        [Authorize(Policy = "ApiAccess")]
        public async Task<IActionResult> TokenGeneration()
        {
            var user = await _userService.GetAsync(User);

            IDictionary<Guid, string> dict = new Dictionary<Guid, string>();
            dict[user.Id] = await _jwtService.GenerateTokenAsync(user);

            return Ok(dict);
        }

        [HttpDelete]
        [Route("", Name = "InvalidateToken")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "ApiAccess")]
        public async Task<IActionResult> TokenInvalidation([FromBody] TokenInvalidationDto dto)
        {
            var user = await _userService.GetAsync(User);
            if (string.IsNullOrEmpty(dto.Token))
                return BadRequest("Invalid parameters or arguments.");

            _jwtService.RevokeToken(User, dto.Token);

            return Ok("Token invalidated successfully.");
        }

        [HttpPut]
        [Route("", Name = "ExtendToken")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "ApiAccess")]
        public async Task<IActionResult> TokenExtension([FromBody] TokenExtensionDto dto)
        {
            var user = await _userService.GetAsync(User);
            if (string.IsNullOrEmpty(dto.Token))
                return BadRequest("Invalid parameters or arguments.");

            _jwtService.ExtendToken(User, dto.Token);

            return Ok("Token extended successfully.");
        }
    }
}

namespace EchoPhase.Models
{
    public class TokenInvalidationDto
    {
        public string? Token
        {
            get; set;
        }
    }

    public class TokenExtensionDto
    {
        public string? Token
        {
            get; set;
        }
    }
}

