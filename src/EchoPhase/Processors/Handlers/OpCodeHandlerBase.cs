using System.Net.WebSockets;
using System.Text.Json;
using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;
using EchoPhase.Processors.Payloads;
using EchoPhase.Services.WebSockets;

namespace EchoPhase.Processors.Handlers
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
            var payload = JsonSerializer.Deserialize<TPayload>(payloadJson, _serializerOptions);

            if (payload is null)
                throw new JsonException($"Failed to deserialize payload for type {typeof(TPayload).Name}.");

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

            if (!validatablePayload.IsValid(out var errorMessage))
            {
                await SendErrorAsync(webSocket, ErrorCodes.InvalidPayload, errorMessage);
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

            await _webSocketService.SendMessageToConnectionAsync(socket, error);
        }
    }
}
