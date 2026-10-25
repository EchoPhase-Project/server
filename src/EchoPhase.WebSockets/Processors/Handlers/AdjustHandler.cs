// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Cluster;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Payloads;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    /// <summary>Replaces the connection's intents.</summary>
    public class AdjustHandler : OpCodeHandlerBase<AdjustPayload>
    {
        private readonly IntentAuthorization _intents;
        private readonly IGatewayBackplane _backplane;

        public AdjustHandler(WebSocketService sockets, IntentAuthorization intents, IGatewayBackplane backplane)
            : base(sockets)
        {
            _intents = intents;
            _backplane = backplane;
        }

        protected override async Task HandleAsync(WebSocketConnection connection, AdjustPayload payload, CancellationToken ct)
        {
            if (_intents.TryApply(connection, payload.Intents) is { } error)
            {
                await Sockets.SendAsync(connection, error, ct);
                return;
            }
            // Its pool's member record carries the intents.
            await _backplane.JoinAsync(connection, ct);
            await Sockets.SendAsync(connection, EventMessage<AdjustAckPayload>.Create(OpCodes.AdjustAck), ct);
        }
    }
}
