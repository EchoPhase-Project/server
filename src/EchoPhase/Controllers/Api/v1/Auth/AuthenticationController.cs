// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Controllers.Api.v1.Common;
using EchoPhase.Identity;
using EchoPhase.RateLimiting;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication;
using EchoPhase.Security.Authorization.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EchoPhase.Controllers.Api.v1.Auth
{
    [Route("api/v1/auth")]
    public class AuthenticationController : ApiControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly IUserService _userService;

        public AuthenticationController(
            IAuthenticationService authService,
            IUserService userService)
        {
            _authService = authService;
            _userService = userService;
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

            return NoContent();
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
