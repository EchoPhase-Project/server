// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authentication.Impersonation;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Server.RateLimiting;
using EchoPhase.Types.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EchoPhase.Server.Controllers.Api.v1.Impersonation
{
    /// <summary>
    /// Acting as another user for support. Tokens are short-lived, carry an
    /// <c>act</c> claim, cannot manage credentials, and every request is logged.
    /// </summary>
    [Route("api/v1/auth/impersonation")]
    public class ImpersonationController : ApiControllerBase
    {
        private readonly IImpersonationService _impersonation;

        public ImpersonationController(IImpersonationService impersonation)
        {
            _impersonation = impersonation;
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
        [ProducesResponseType<ImpersonationStarted>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Start([FromBody] StartImpersonationRequest dto, CancellationToken ct = default)
        {
            var result = await _impersonation.StartAsync(User.GetUserId(), dto.Username, dto.Reason, dto.Password, ct);
            return result.Successful ? Ok(result.Value) : Failure(result.Error);
        }

        /// <summary>Ends an impersonation you started, from your own session.</summary>
        [HttpDelete("{id:guid}")]
        [RequirePrincipal(PrincipalKind.Session)]
        [ValidateAntiForgery]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> End(Guid id, CancellationToken ct = default)
        {
            var result = await _impersonation.EndAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        /// <summary>Ends the impersonation this token belongs to.</summary>
        [HttpDelete("current")]
        [RequirePrincipal(PrincipalKind.Impersonation)]
        [ValidateAntiForgery]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
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
        [ProducesResponseType<ImpersonationResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List(CancellationToken ct = default)
            => Ok((await _impersonation.ListAsync(User.GetUserId(), ct)).Select(ImpersonationResponse.From).ToList());
    }

    /// <summary>An impersonation the caller took part in, as actor or as target.</summary>
    /// <param name="Id">Its id.</param>
    /// <param name="ActorId">Who acted.</param>
    /// <param name="TargetId">Whom they acted as.</param>
    /// <param name="Reason">Why, as the actor wrote it.</param>
    /// <param name="ExpiresAt">When its tokens stop working.</param>
    /// <param name="EndedAt">When it was ended; none: not ended (it may have expired).</param>
    /// <param name="CreatedAt">When it started.</param>
    /// <param name="UpdatedAt">The last change.</param>
    public sealed record ImpersonationResponse(
        Guid Id, Guid ActorId, Guid TargetId, string Reason, DateTime ExpiresAt, DateTime? EndedAt,
        DateTime CreatedAt, DateTime UpdatedAt)
    {
        public static ImpersonationResponse From(DAL.Postgres.Models.Impersonation i) => new(
            i.Id, i.ActorId, i.TargetId, i.Reason, i.ExpiresAt, i.EndedAt, i.CreatedAt, i.UpdatedAt);
    }
}
