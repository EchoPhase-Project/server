// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Payloads;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    /// <summary>Replaces the connection's intents.</summary>
    public class AdjustHandler : OpCodeHandlerBase<AdjustPayload>
    {
        private readonly IntentAuthorization _intents;

        public AdjustHandler(WebSocketService sockets, IntentAuthorization intents)
            : base(sockets)
        {
            _intents = intents;
        }

        protected override Task HandleAsync(WebSocketConnection connection, AdjustPayload payload, CancellationToken ct)
            => _intents.TryApply(connection, payload.Intents) is { } error
                ? Sockets.SendAsync(connection, error, ct)
                : Sockets.SendAsync(connection, EventMessage<AdjustAckPayload>.Create(OpCodes.AdjustAck), ct);
    }
}
