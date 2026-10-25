// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Discord.WebSocket;
using EchoPhase.Connections;
using EchoPhase.Connections.Services;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Discord
{
    [Route("api/v1/discord/gateway")]
    public class DiscordGatewayController : ApiControllerBase
    {
        private readonly DiscordWebSocketManager _manager;
        private readonly IConnectionService _connections;

        public DiscordGatewayController(
            DiscordWebSocketManager manager,
            IConnectionService connections)
        {
            _manager = manager;
            _connections = connections;
        }

        /// <summary>Opens a gateway connection with the named Discord bot connection.</summary>
        [HttpPost("connections")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Discord, Permissions.Connect)]
        public async Task<IActionResult> Connect([FromBody] GatewayConnectRequest dto, CancellationToken ct = default)
        {
            var userId = User.GetUserId();

            // The gateway speaks WebSocket, not HTTP, so the bot token is
            // resolved here instead of by the HttpClient auth handler.
            var credential = await _connections.ResolveAsync(
                new ConnectionRef(userId, dto.Name), ConnectionProviders.Discord, ct);
            if (!credential.Successful)
                return Failure(credential.Error);

            var connectionId = await _manager.ConnectAsync(
                userId, dto.Name, credential.Value.AccessToken, dto.Intents ?? DiscordGatewayIntents.Default, ct);

            return Ok(new { connectionId });
        }

        /// <summary>Closes one of the caller's gateway connections.</summary>
        [HttpDelete("connections/{connectionId:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Discord, Permissions.Connect)]
        public async Task<IActionResult> Disconnect(Guid connectionId)
        {
            var userId = User.GetUserId();

            await _manager.DisconnectAsync(userId, connectionId);
            return NoContent();
        }

        /// <summary>The caller's open gateway connections.</summary>
        [HttpGet("connections")]
        [RequirePermission(Resources.Discord, Permissions.Connect)]
        public IActionResult GetConnections()
        {
            var userId = User.GetUserId();

            var connections = _manager.GetConnections(userId);
            return Ok(connections);
        }
    }
}
