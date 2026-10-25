// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Identity;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Server.RateLimiting;
using EchoPhase.Server.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EchoPhase.Server.Controllers.Api.v1.Auth
{
    [Route("api/v1/auth")]
    public class AuthenticationController : ApiControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly IUserService _userService;
        private readonly UserManager<User> _users;
        private readonly NotificationEventsNotifier _notifier;
        private readonly ILogger<AuthenticationController> _logger;

        public AuthenticationController(
            IAuthenticationService authService,
            IUserService userService,
            UserManager<User> users,
            NotificationEventsNotifier notifier,
            ILogger<AuthenticationController> logger)
        {
            _authService = authService;
            _userService = userService;
            _users = users;
            _notifier = notifier;
            _logger = logger;
        }

        /// <summary>Creates the account and signs in (sets the token cookies).</summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<IReadOnlyList<ErrorBody>>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest dto, CancellationToken ct = default)
        {
            var registration = await _userService.CreateUserAsync(dto.Name, dto.Username, dto.Password, dto.TimeZone);
            if (!registration.Succeeded)
                return BadRequest(registration.Errors.Select(ErrorBody.From).ToList());

            var auth = await _authService.LoginAsync(dto.Username, dto.Password, ct);
            if (!auth.Successful)
                return Failure(auth.Error);

            return NoContent();
        }

        /// <summary>Signs in: sets the token cookies and returns the CSRF token in X-CSRF-TOKEN.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<IReadOnlyList<ErrorBody>>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest dto, CancellationToken ct = default)
        {
            var result = await _authService.LoginAsync(dto.Username, dto.Password, ct);
            if (!result.Successful)
                return Failure(result.Error);

            await TellAboutSignInAsync(dto.Username, ct);
            return NoContent();
        }

        // The account's owner hears of every sign-in, so a stranger's stands out.
        private async Task TellAboutSignInAsync(string username, CancellationToken ct)
        {
            try
            {
                var user = await _users.FindByNameAsync(username);
                if (user is not null)
                    await _notifier.SignedInAsync(user.Id, Request.Headers.UserAgent.ToString(), ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger.LogWarning(e, "Could not tell about a sign-in");
            }
        }

        [HttpGet("me")]
        // No type: taken from ActionResult<T>, which the compiler checks.
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<MeResponse>> Me()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            return MeResponse.From(user);
        }

        /// <summary>Changes the caller's own profile: name and/or time zone.</summary>
        [HttpPatch("me")]
        [ValidateAntiForgery]
        [RequirePrincipal(PrincipalKind.Session)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<IReadOnlyList<ErrorBody>>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<MeResponse>> UpdateMe([FromBody] UpdateProfileRequest dto)
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _userService.UpdateProfileAsync(user, dto.Name, dto.TimeZone);
            if (!result.Succeeded)
                return BadRequest(result.Errors.Select(ErrorBody.From).ToList());

            return MeResponse.From(user);
        }
    }
}
