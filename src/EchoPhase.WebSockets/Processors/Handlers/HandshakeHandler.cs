// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.WebSocket;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Payloads;
using Microsoft.Extensions.Options;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    /// <summary>Optionally sets intents; answers with them and the heartbeat interval.</summary>
    public class HandshakeHandler : OpCodeHandlerBase<HandshakePayload>
    {
        private readonly IntentAuthorization _intents;
        private readonly WebSocketOptions _settings;

        public HandshakeHandler(WebSocketService sockets, IntentAuthorization intents, IOptions<WebSocketOptions> settings)
            : base(sockets)
        {
            _intents = intents;
            _settings = settings.Value;
        }

        protected override async Task HandleAsync(WebSocketConnection connection, HandshakePayload payload, CancellationToken ct)
        {
            if (payload.Intents is not null && _intents.TryApply(connection, payload.Intents) is { } error)
            {
                await Sockets.SendAsync(connection, error, ct);
                return;
            }

            await Sockets.SendAsync(connection, EventMessage<HandshakeAckPayload>.Create(OpCodes.HandshakeAck, p =>
            {
                p.HeartbeatInterval = _settings.HeartbeatInterval.TotalMilliseconds;
                p.Intents = _intents.Names(connection.Intents);
            }), ct);
        }
    }
}
