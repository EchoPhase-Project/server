using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Identity;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Jwt;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
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

        [HttpPost("{guid:guid}")]
        [BearerOrValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateById(Guid guid)
        {
            var users = _userService.Get(x => x.Ids = [guid]);
            if (!users.Data.Any())
                return NotFound();
            return Ok(await BuildTokenDictAsync(users.Data));
        }

        [HttpPost("{username:username}")]
        [BearerOrValidateAntiForgeryToken]
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
            var result = new Dictionary<Guid, string>();
            foreach (var user in users.ToList())
                result[user.Id] = await _jwt.GenerateTokenAsync(user);
            return result;
        }
    }
}
