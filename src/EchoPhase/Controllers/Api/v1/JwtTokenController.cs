using EchoPhase.Identity;
using EchoPhase.Security.Authentication.Jwt;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.AspNetCore.Mvc;
using ParkSquare.AspNetCore.Sitemap;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [SitemapExclude]
    [Route("/api/v1/auth/jwt")]
    public class JwtTokenController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IJwtTokenProvider _jwt;

        public JwtTokenController(IUserService userService, IJwtTokenProvider jwt)
        {
            _userService = userService;
            _jwt = jwt;
        }

        [HttpPost("@me")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateForSelf()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            return Ok(await BuildTokenDictAsync([user]));
        }

        [HttpPost("{guid:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateById(Guid guid)
        {
            var users = _userService.Get(x => x.Ids = [guid]);
            if (!users.Data.Any())
                return NotFound();
            return Ok(await BuildTokenDictAsync(users.Data));
        }

        [HttpPost("{username:username}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required.");
            var users = _userService.Get(x => x.UserNames = [username]);
            if (!users.Data.Any())
                return NotFound();
            return Ok(await BuildTokenDictAsync(users.Data));
        }

        private async Task<IDictionary<Guid, string>> BuildTokenDictAsync(IEnumerable<User> users)
        {
            var tasks = users.Select(async user => (user.Id, Token: await _jwt.GenerateTokenAsync(user)));
            var results = await Task.WhenAll(tasks);
            return results.ToDictionary(x => x.Id, x => x.Token);
        }
    }
}
