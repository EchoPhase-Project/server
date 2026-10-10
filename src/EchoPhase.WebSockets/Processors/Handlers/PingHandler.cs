// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Payloads;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    /// <summary>Answers Pong. (Any message keeps the connection alive; Ping is for idle clients.)</summary>
    public class PingHandler : OpCodeHandlerBase<PingPayload>
    {
        public PingHandler(WebSocketService sockets)
            : base(sockets)
        {
        }

        protected override Task HandleAsync(WebSocketConnection connection, PingPayload payload, CancellationToken ct)
            => Sockets.SendAsync(connection, EventMessage.Create(OpCodes.Pong), ct);
    }
}
