// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Discord.WebSocket;
using EchoPhase.Security.Authentication.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    public class DiscordGatewayController : ControllerBase
    {
        private readonly DiscordWebSocketManager _manager;
        private readonly ILogger<DiscordGatewayController> _logger;

        public DiscordGatewayController(
            DiscordWebSocketManager manager,
            ILogger<DiscordGatewayController> logger)
        {
            _manager = manager;
            _logger = logger;
        }

        [HttpPost("connect")]
        public async Task<IActionResult> Connect([FromBody] GatewayConnectDto dto)
        {
            var userId = User.GetUserId();

            var connectionId = await _manager.ConnectAsync(userId, dto.Name);

            return Ok(new { connectionId });
        }

        [HttpDelete("disconnect/{connectionId:guid}")]
        public async Task<IActionResult> Disconnect(Guid connectionId)
        {
            var userId = User.GetUserId();

            await _manager.DisconnectAsync(userId, connectionId);
            return NoContent();
        }

        [HttpGet("connections")]
        public IActionResult GetConnections()
        {
            var userId = User.GetUserId();

            var connections = _manager.GetConnections(userId);
            return Ok(connections);
        }
    }

    public class GatewayConnectDto
    {
        public string Name { get; set; } = string.Empty;
    }
}
