// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using System.Text.Json;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Payloads;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    public abstract class OpCodeHandlerBase<TPayload> : IOpCodeHandler<TPayload>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly WebSocketService _webSocketService;
        private readonly JsonSerializerOptions _serializerOptions = new();

        protected OpCodeHandlerBase(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _webSocketService = GetService<WebSocketService>();
        }

        protected T GetService<T>() where T : notnull
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        public async Task HandleAsync(WebSocket webSocket, string payloadJson)
        {
            var payload = JsonSerializer.Deserialize<TPayload>(payloadJson, _serializerOptions) ?? throw new JsonException($"Failed to deserialize payload for type {typeof(TPayload).Name}.");
            await BeforeHandleAsync(webSocket, payload);
        }

        public async Task HandleAsync(WebSocket webSocket, object payload)
        {
            TPayload? typedPayload = default;

            if (payload is null)
                throw new ArgumentNullException("Missing payload exception.");

            if (payload is JsonElement jsonElement)
                typedPayload = jsonElement.Deserialize<TPayload>();
            else if (payload is TPayload)
                typedPayload = (TPayload)payload;

            if (typedPayload is null)
                throw new InvalidCastException($"Invalid payload type. Expected {typeof(TPayload)}, got {payload?.GetType()}.");

            await BeforeHandleAsync(webSocket, typedPayload);
        }

        private async Task BeforeHandleAsync(WebSocket webSocket, TPayload payload)
        {
            if (await ValidatePayloadAsync(webSocket, payload))
                await HandleAsync(webSocket, payload);
        }

        public abstract Task HandleAsync(WebSocket webSocket, TPayload payload);

        private async Task<bool> ValidatePayloadAsync(WebSocket webSocket, TPayload payload)
        {
            if (payload is null)
            {
                await SendErrorAsync(webSocket, ErrorCodes.InvalidPayload, "Missing payload.");
                return false;
            }

            return await ValidatePayloadAsync(webSocket, (object)payload);
        }

        private async Task<bool> ValidatePayloadAsync(WebSocket webSocket, object payload)
        {
            if (payload is not IPayload validatablePayload)
            {
                await SendErrorAsync(webSocket, ErrorCodes.InvalidPayload, "Payload is not validatable.");
                return false;
            }

            var validationResult = validatablePayload.Validate();
            if (!validationResult.Successful)
            {
                await SendErrorAsync(webSocket, ErrorCodes.InvalidPayload, validationResult.Error.Value);
                return false;
            }

            return true;
        }

        private async Task SendErrorAsync(WebSocket socket, ErrorCodes code, string msg)
        {
            var error = EventMessage<ErrorPayload>.Create(OpCodes.Error, e =>
            {
                e.Code = code;
                e.Message = msg;
            });

            await _webSocketService.SendMessageAsync(socket, error);
        }
    }
}
