// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.ApiClients;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.ApiClients
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

        public ApiClientsController(IApiClientService clients)
        {
            _clients = clients;
        }

        [HttpGet]
        [RequirePermission(Resources.ApiClients, Permissions.Read)]
        [ProducesResponseType<ApiClientResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List(CancellationToken ct = default)
        {
            var clients = await _clients.ListAsync(User.GetUserId(), ct);
            return Ok(clients.Select(ApiClientResponse.From).ToList());
        }

        /// <summary>
        /// Creates a client. The secret is returned once and cannot be retrieved later.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgery]
        [RequirePermission(Resources.ApiClients, Permissions.Create)]
        [ProducesResponseType<CreatedApiClientResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateApiClientRequest dto, CancellationToken ct = default)
        {
            var grant = new ApiClientGrant(
                dto.Claims?.Permissions ?? new Dictionary<string, string[]>());

            var result = await _clients.CreateAsync(User.GetUserId(), dto.Name, grant, dto.ExpiresAt, ct);
            if (!result.Successful)
                return Failure(result.Error);

            return Ok(new CreatedApiClientResponse(ApiClientResponse.From(result.Value.Client), result.Value.ClientSecret));
        }

        /// <summary>
        /// Issues a new secret; the old one keeps working until the previous
        /// secret is revoked, so running instances can switch over first.
        /// </summary>
        [HttpPost("{id:guid}/secret")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.ApiClients, Permissions.Update)]
        [ProducesResponseType<ApiClientSecretResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RotateSecret(Guid id, CancellationToken ct = default)
        {
            var result = await _clients.RotateSecretAsync(User.GetUserId(), id, ct);
            return result.Successful ? Ok(new ApiClientSecretResponse(result.Value)) : Failure(result.Error);
        }

        [HttpDelete("{id:guid}/secret/previous")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.ApiClients, Permissions.Update)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
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
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var result = await _clients.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }
    }
}
