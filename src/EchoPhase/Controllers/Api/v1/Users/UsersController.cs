// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using EchoPhase.Projection;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1.Users
{
    [Route("api/v1/users")]
    public class UsersController : ApiControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserRepository _userRepository;
        private readonly Projector _projector;

        public UsersController(
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
        [RequirePermission(Resources.Users, Permissions.Read)]
        public async Task<IActionResult> GetUser(string username, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required.");

            var users = await _userRepository.Query()
                .WithUserNames(username)
                .ToListAsync(ct);

            if (users.Count == 0)
                return NotFound();

            return Ok(users.Select(user =>
                _projector.For(user).Only(u => u.Id, u => u.Name, u => u.UserName).Build()));
        }
    }
}
