using EchoPhase.Helpers;
using EchoPhase.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAntiforgeryService _antiforgeryService;
        private readonly ProjectionHelper _projection;

        public UserController(
            IUserService userService,
            IAntiforgeryService antiforgeryService,
            ProjectionHelper projection
        )
        {
            _userService = userService;
            _antiforgeryService = antiforgeryService;
            _projection = projection;
        }

        [HttpGet("{username}")]
        public IActionResult GetUser(string username)
        {
            var users = _userService.Get(x =>
                {
                    x.UserNames = new HashSet<string>() { username };
                });

            return Ok(users
                .Select(user =>
                    _projection.Project(user, u => u.Id, u => u.Name, u => u.UserName)
                ));
        }
    }
}
