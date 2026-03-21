// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Repositories;
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
        private readonly UserRepository _userRepository;
        private readonly Projector _projector;

        public UserController(
            IUserService userService,
            UserRepository userRepository,
            Projector projector
        )
        {
            _userService = userService;
            _userRepository = userRepository;
            _projector = projector;
        }

        [HttpGet("{username:username}")]
        public IActionResult GetUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required.");

            var users = _userRepository.Query()
                .WithUserNames(username)
                .ToList();

            if (!users.Any())
                return NotFound();

            return Ok(users.Select(user =>
                _projector.For(user).Include(u => u.Id, u => u.Name, u => u.UserName).Build()));
        }
    }
}
