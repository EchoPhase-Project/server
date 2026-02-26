using EchoPhase.Identity;
using EchoPhase.Projection;
using EchoPhase.Security.Antiforgery;
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
        private readonly Projector _projector;

        public UserController(
            IUserService userService,
            IAntiforgeryService antiforgeryService,
            Projector projector
        )
        {
            _userService = userService;
            _antiforgeryService = antiforgeryService;
            _projector = projector;
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
                    _projector.Project(user, u => u.Id, u => u.Name, u => u.UserName)
                ));
        }
    }
}
