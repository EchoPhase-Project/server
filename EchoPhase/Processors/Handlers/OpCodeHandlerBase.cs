using System.Net.WebSockets;
using System.Text.Json;
using EchoPhase.Interfaces;
using EchoPhase.Models;
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
            await ValidatePayloadAsync(webSocket, payload);

            await HandleAsync(webSocket, payload);
        }

        public abstract Task HandleAsync(WebSocket webSocket, TPayload payload);

        private async Task ValidatePayloadAsync(WebSocket webSocket, TPayload payload)
        {
            if (payload is null)
                throw new ArgumentNullException("Missing payload exception.");

            await ValidatePayloadAsync(webSocket, (object)payload);
        }

        private async Task ValidatePayloadAsync(WebSocket webSocket, object payload)
        {
            if (payload is not IPayload validatablePayload)
                throw new InvalidOperationException("Payload is not validatable.");

            string errorMessage = string.Empty;

            if (validatablePayload.IsValid(out errorMessage))
                return;

            EventMessage response = new EventMessage()
            {
                Op = OpCodes.Error,
                D = new ErrorPayload()
                {
                    Code = ErrorCodes.InvalidMessage,
                    Message = errorMessage
                }
            };

            await _webSocketService.SendMessageToConnectionAsync(webSocket, response);

            throw new InvalidOperationException(errorMessage);
        }
    }
}
