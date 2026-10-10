// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Payloads;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    /// <summary>Acknowledges and closes the connection.</summary>
    public class DisconnectHandler : OpCodeHandlerBase<DisconnectPayload>
    {
        private readonly WebSocketConnectionManager _connections;

        public DisconnectHandler(WebSocketService sockets, WebSocketConnectionManager connections)
            : base(sockets)
        {
            _connections = connections;
        }

        protected override async Task HandleAsync(WebSocketConnection connection, DisconnectPayload payload, CancellationToken ct)
        {
            await Sockets.SendAsync(connection, EventMessage.Create(OpCodes.DisconnectAck), ct);
            await _connections.CloseConnectionAsync(connection, WebSocketCloseStatus.NormalClosure, "Disconnect requested");
        }
    }
}
