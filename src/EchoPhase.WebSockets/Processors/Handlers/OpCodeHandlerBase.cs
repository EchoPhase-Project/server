// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Payloads;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    /// <summary>
    /// Binds "d" to <typeparamref name="TPayload"/> (camelCase or any case,
    /// as the server's own messages are camelCase), validates it, then handles
    /// it. A bad payload is answered with an Error message, not an exception.
    /// </summary>
    public abstract class OpCodeHandlerBase<TPayload> : IOpCodeHandler
        where TPayload : class, IPayload, new()
    {
        protected OpCodeHandlerBase(WebSocketService sockets)
        {
            Sockets = sockets;
        }

        protected WebSocketService Sockets { get; }

        public async Task HandleAsync(WebSocketConnection connection, JsonElement payload, CancellationToken ct = default)
        {
            TPayload? typed;
            try
            {
                typed = payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                    ? new TPayload()
                    : payload.Deserialize<TPayload>(WebSocketService.JsonOptions);
            }
            catch (JsonException ex)
            {
                await SendErrorAsync(connection, ErrorCodes.DeserializationError, ex.Message, ct);
                return;
            }

            if (typed is null)
            {
                await SendErrorAsync(connection, ErrorCodes.InvalidPayload, "Missing payload.", ct);
                return;
            }

            var validation = typed.Validate();
            if (!validation.Successful)
            {
                await SendErrorAsync(connection, ErrorCodes.InvalidPayload, validation.Error.Message, ct);
                return;
            }

            await HandleAsync(connection, typed, ct);
        }

        protected abstract Task HandleAsync(WebSocketConnection connection, TPayload payload, CancellationToken ct);

        protected Task SendErrorAsync(WebSocketConnection connection, ErrorCodes code, string message, CancellationToken ct)
            => Sockets.SendAsync(connection, ErrorMessages.Create(code, message), ct);
    }
}
