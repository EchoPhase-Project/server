using System.Net.WebSockets;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result.Extensions;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Exceptions;
using EchoPhase.WebSockets.Processors.Payloads;

namespace EchoPhase.WebSockets.Processors.Handlers
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

                var result = IntentsBitMask.Deserialize(payload.Intents);

                if (result.TryGetError(out var error))
                {
                    var responseError = EventMessage<ErrorPayload>.Create(OpCodes.Error, err =>
                    {
                        err.Code = ErrorCodes.DeserializationError;
                        err.Message = error.Value;
                    });

                    await _webSocketService.SendMessageAsync(webSocket, responseError);
                    return;
                }

                var response = EventMessage<AdjustAckPayload>.Create(OpCodes.AdjustAck);

                await _webSocketService.SendMessageAsync(webSocket, response);
            }
            catch (WebSocketConnectionNotFoundException)
            {
                var response = EventMessage<ErrorPayload>.Create(OpCodes.Error, p =>
                {
                    p.Code = ErrorCodes.NotFound;
                    p.Message = "Connection not found.";
                });

                await _webSocketService.SendMessageAsync(webSocket, response);

                throw;
            }

            return;
        }
    }
}
