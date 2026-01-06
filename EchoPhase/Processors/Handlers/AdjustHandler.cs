using System.Net.WebSockets;
using EchoPhase.Attributes;
using EchoPhase.Exceptions;
using EchoPhase.Extensions;
using EchoPhase.Processors.Enums;
using EchoPhase.Processors.Payloads;
using EchoPhase.Services.Bitmasks;
using EchoPhase.Services.WebSockets;

namespace EchoPhase.Processors.Handlers
{
    [OpCodeHandler(OpCodes.Adjust)]
    public class AdjustHandler : OpCodeHandlerBase<AdjustPayload>
    {
        private readonly WebSocketService _webSocketService;
        private readonly WebSocketConnectionManager _connectionManager;

        public AdjustHandler(IServiceProvider serviceProvider)
        : base(serviceProvider)
        {
            _webSocketService = GetService<WebSocketService>();
            _connectionManager = GetService<WebSocketConnectionManager>();
        }

        public override async Task HandleAsync(WebSocket webSocket, AdjustPayload payload)
        {
            try
            {
                var connection = await _connectionManager.GetConnectionAsync(webSocket);

                var result = IntentsBitMaskService.Deserialize(payload.Intents);

                if (result.TryGetError(out var error))
                {
                    var responseError = EventMessage<ErrorPayload>.Create(OpCodes.Error, err =>
                    {
                        err.Code = ErrorCodes.DeserializationError;
                        err.Message = error.Value;
                    });

                    await _webSocketService.SendMessageToConnectionAsync(webSocket, responseError);
                    return;
                }

                var response = EventMessage<AdjustAckPayload>.Create(OpCodes.AdjustAck);

                await _webSocketService.SendMessageToConnectionAsync(webSocket, response);
            }
            catch (WebSocketConnectionNotFoundException)
            {
                var response = EventMessage<ErrorPayload>.Create(OpCodes.Error, p =>
                {
                    p.Code = ErrorCodes.NotFound;
                    p.Message = "Connection not found.";
                });

                await _webSocketService.SendMessageToConnectionAsync(webSocket, response);

                throw;
            }

            return;
        }
    }
}
