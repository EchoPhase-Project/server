// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.ApiClients;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1.ApiClients
{
    /// <summary>
    /// Credentials for bots, scripts and servers (OAuth client-credentials grant).
    /// Exchange them at <c>POST /api/v1/auth/token</c>.
    /// </summary>
    [Route("api/v1/api-clients")]
    [RequirePrincipal(PrincipalKind.Session)]
    public class ApiClientsController : ApiControllerBase
    {
        private readonly IApiClientService _clients;
        private readonly Projector _projector;

        public ApiClientsController(IApiClientService clients, Projector projector)
        {
            _clients = clients;
            _projector = projector;
        }

        [HttpGet]
        [RequirePermission(Resources.ApiClients, Permissions.Read)]
        public async Task<IActionResult> List(CancellationToken ct = default)
        {
            var clients = await _clients.ListAsync(User.GetUserId(), ct);
            return Ok(_projector.ForCollection(clients).Build());
        }

        /// <summary>
        /// Creates a client. The secret is returned once and cannot be retrieved later.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgery]
        [RequirePermission(Resources.ApiClients, Permissions.Create)]
        public async Task<IActionResult> Create([FromBody] CreateApiClientRequest dto, CancellationToken ct = default)
        {
            var grant = new ApiClientGrant(
                dto.Claims?.Permissions ?? new Dictionary<string, string[]>());

            var result = await _clients.CreateAsync(User.GetUserId(), dto.Name, grant, dto.ExpiresAt, ct);
            if (!result.Successful)
                return Failure(result.Error);

            return Ok(new
            {
                client = _projector.For(result.Value!.Client).Build(),
                clientSecret = result.Value.ClientSecret
            });
        }

        /// <summary>
        /// Issues a new secret; the old one keeps working until the previous
        /// secret is revoked, so running instances can switch over first.
        /// </summary>
        [HttpPost("{id:guid}/secret")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.ApiClients, Permissions.Update)]
        public async Task<IActionResult> RotateSecret(Guid id, CancellationToken ct = default)
        {
            var result = await _clients.RotateSecretAsync(User.GetUserId(), id, ct);
            return result.Successful ? Ok(new { clientSecret = result.Value }) : Failure(result.Error);
        }

        [HttpDelete("{id:guid}/secret/previous")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.ApiClients, Permissions.Update)]
        public async Task<IActionResult> RevokePreviousSecret(Guid id, CancellationToken ct = default)
        {
            var result = await _clients.RevokePreviousSecretAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        /// <summary>
        /// Deletes the client; tokens already issued to it stop working immediately.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.ApiClients, Permissions.Delete)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var result = await _clients.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        private ObjectResult Failure(IServiceError error) => Error(error, error.Code == ApiClientErrors.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest);
    }
}
