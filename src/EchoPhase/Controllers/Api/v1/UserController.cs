using EchoPhase.Identity;
using EchoPhase.Projection;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly Projector _projector;

        public UserController(IUserService userService, Projector projector)
        {
            _userService = userService;
            _projector = projector;
        }

        [HttpGet("@me")]
        public async Task<IActionResult> GetUser()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            return Ok(_projector.Project(user, u => u.UserName, u => u.Id, u => u.Name));
        }

        [HttpGet("{username:username}")]
        public IActionResult GetUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required.");

            var users = _userService.Get(x => x.UserNames = [username]);
            if (!users.Any())
                return NotFound();

            return Ok(users.Select(user =>
                _projector.Project(user, u => u.Id, u => u.Name, u => u.UserName)));
        }
    }
}
