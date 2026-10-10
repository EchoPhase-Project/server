// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Handlers;
using EchoPhase.WebSockets.Processors.Payloads;

namespace EchoPhase.WebSockets.Processors
{
    /// <summary>
    /// Dispatches one client message, { "op": n, "d": { ... } }, to the
    /// handler for its op. Malformed messages and unknown ops are answered
    /// with an Error message; the connection stays open.
    /// </summary>
    public class WebSocketProcessor
    {
        private readonly WebSocketService _sockets;
        private readonly OpCodeHandlerResolver _handlers;

        public WebSocketProcessor(WebSocketService sockets, OpCodeHandlerResolver handlers)
        {
            _sockets = sockets;
            _handlers = handlers;
        }

        public async Task HandleMessageAsync(WebSocketConnection connection, ReadOnlyMemory<byte> utf8Json, CancellationToken ct = default)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(utf8Json);
            }
            catch (JsonException ex)
            {
                await SendErrorAsync(connection, ErrorCodes.DeserializationError, $"Invalid JSON: {ex.Message}", ct);
                return;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !TryGetProperty(root, "op", out var opElement)
                    || !opElement.TryGetInt32(out var op))
                {
                    await SendErrorAsync(connection, ErrorCodes.InvalidMessage, "Expected { \"op\": number, \"d\": object }.", ct);
                    return;
                }

                if (!Enum.IsDefined((OpCodes)op) || _handlers.Find((OpCodes)op) is not { } handler)
                {
                    await SendErrorAsync(connection, ErrorCodes.Unsupported, $"Unsupported op {op}.", ct);
                    return;
                }

                TryGetProperty(root, "d", out var payload);
                await handler.HandleAsync(connection, payload, ct);
            }
        }

        // Clients may send "op" or "Op": the server writes camelCase, older clients wrote PascalCase.
        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }

            value = default;
            return false;
        }

        private Task SendErrorAsync(WebSocketConnection connection, ErrorCodes code, string message, CancellationToken ct)
            => _sockets.SendAsync(connection, ErrorMessages.Create(code, message), ct);
    }
}
