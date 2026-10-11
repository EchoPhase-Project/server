// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection;
using EchoPhase.RateLimiting;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authentication.Impersonation;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Types.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EchoPhase.Controllers.Api.v1.Impersonation
{
    /// <summary>
    /// Acting as another user for support. Tokens are short-lived, carry an
    /// <c>act</c> claim, cannot manage credentials, and every request is logged.
    /// </summary>
    [Route("api/v1/auth/impersonation")]
    public class ImpersonationController : ApiControllerBase
    {
        private readonly IImpersonationService _impersonation;
        private readonly Projector _projector;

        public ImpersonationController(IImpersonationService impersonation, Projector projector)
        {
            _impersonation = impersonation;
            _projector = projector;
        }

        /// <summary>
        /// Starts impersonating a user with a lower role. Send the returned token
        /// as <c>Authorization: Bearer</c>; your own session is not affected.
        /// </summary>
        [HttpPost]
        [RequirePrincipal(PrincipalKind.Session)]
        [RequirePermission(Resources.Users, Permissions.Impersonate)]
        [ValidateAntiForgery]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        public async Task<IActionResult> Start([FromBody] StartImpersonationRequest dto, CancellationToken ct = default)
        {
            var result = await _impersonation.StartAsync(User.GetUserId(), dto.Username, dto.Reason, dto.Password, ct);
            return result.Successful ? Ok(result.Value) : Failure(result.Error);
        }

        /// <summary>Ends an impersonation you started, from your own session.</summary>
        [HttpDelete("{id:guid}")]
        [RequirePrincipal(PrincipalKind.Session)]
        [ValidateAntiForgery]
        public async Task<IActionResult> End(Guid id, CancellationToken ct = default)
        {
            var result = await _impersonation.EndAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        /// <summary>Ends the impersonation this token belongs to.</summary>
        [HttpDelete("current")]
        [RequirePrincipal(PrincipalKind.Impersonation)]
        [ValidateAntiForgery]
        public async Task<IActionResult> EndCurrent(CancellationToken ct = default)
        {
            if (!Guid.TryParse(User.FindFirst(EchoPhaseClaimTypes.ImpersonationId)?.Value, out var id))
                return BadRequest();

            var result = await _impersonation.EndCurrentAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        /// <summary>
        /// Recent impersonations you took part in: started by you, or of your account.
        /// </summary>
        [HttpGet]
        [RequirePrincipal(PrincipalKind.Session)]
        [RequirePermission(Resources.Sessions, Permissions.Read)]
        public async Task<IActionResult> List(CancellationToken ct = default)
            => Ok(_projector.ForCollection(await _impersonation.ListAsync(User.GetUserId(), ct)).Build());
    }
}
