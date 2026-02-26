using EchoPhase.Identity;
using EchoPhase.Security.Authentication;
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
        [Authorize(Policy = "DevOrHigher")]
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
        [Authorize(Policy = "DevOrHigher")]
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
        [Authorize(Policy = "DevOrHigher")]
        public async Task<IActionResult> TokenGeneration()
        {
            var user = await _userService.GetAsync(User);

            IDictionary<Guid, string> dict = new Dictionary<Guid, string>();
            dict[user.Id] = await _jwtService.GenerateTokenAsync(user);

            return Ok(dict);
        }
    }
}
