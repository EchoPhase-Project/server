// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Payloads;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    [OpCodeHandler(OpCodes.Ping)]
    public class PingHandler : OpCodeHandlerBase<PingPayload>
    {
        private readonly WebSocketService _webSocketService;
        private readonly WebSocketConnectionManager _connectionManager;

        public PingHandler(IServiceProvider serviceProvider)
        : base(serviceProvider)
        {
            _webSocketService = GetService<WebSocketService>();
            _connectionManager = GetService<WebSocketConnectionManager>();
        }

        public override async Task HandleAsync(WebSocket webSocket, PingPayload payload)
        {
            await _connectionManager.RefreshHeartbeatAsync(webSocket);

            var response = EventMessage.Create(OpCodes.Pong);

            await _webSocketService.SendMessageAsync(webSocket, response);
        }
    }
}
