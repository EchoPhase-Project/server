// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Users
{
    [Route("api/v1/users")]
    public class UsersController : ApiControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserRepository _userRepository;

        public UsersController(
            IUserService userService,
            UserRepository userRepository
        )
        {
            _userService = userService;
            _userRepository = userRepository;
        }

        [HttpGet("{username:username}")]
        [RequirePermission(Resources.Users, Permissions.Read)]
        [ProducesResponseType<UserSummaryResponse[]>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUser(string username, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(username))
                return BadRequest("Username is required.");

            var users = await _userRepository.Query()
                .WithUserNames(username)
                .ToListAsync(ct);

            if (users.Count == 0)
                return NotFound();

            return Ok(users.Select(UserSummaryResponse.From).ToList());
        }
    }

    /// <summary>A user as others see them: the id and the names, nothing private.</summary>
    /// <param name="Id">The user's id.</param>
    /// <param name="UserName">The unique user name.</param>
    /// <param name="Name">The display name.</param>
    public sealed record UserSummaryResponse(Guid Id, string? UserName, string Name)
    {
        public static UserSummaryResponse From(User user) => new(user.Id, user.UserName, user.Name);
    }
}
