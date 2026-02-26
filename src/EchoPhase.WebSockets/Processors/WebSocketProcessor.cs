using System.Net.WebSockets;
using System.Text.Json;
using EchoPhase.Identity;
using EchoPhase.WebSockets.Processors.Handlers;
using EchoPhase.WebSockets.Processors.Payloads;
using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors
{
    public class WebSocketProcessor
    {
        private readonly WebSocketService _webSocketService;
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly IUserService _userService;
        private readonly OpCodeHandlerResolver _handlerResolver;

        public WebSocketProcessor(
            WebSocketService webSocketService,
            WebSocketConnectionManager connectionManager,
            IUserService userService,
            OpCodeHandlerResolver handlerResolver
        )
        {
            _webSocketService = webSocketService;
            _connectionManager = connectionManager;
            _userService = userService;
            _handlerResolver = handlerResolver;
        }

        public async Task HandleMessageAsync(WebSocket webSocket, string message)
        {
            var rawMessage = JsonSerializer.Deserialize<RawEventMessage>(message);
            if (rawMessage == null)
            {
                await SendErrorAsync(webSocket, ErrorCodes.DeserializationError, "Unable to deserialize JSON.");
                return;
            }

            if (rawMessage.D == null)
            {
                await SendErrorAsync(webSocket, ErrorCodes.InvalidPayload, "Payload data is missing.");
                return;
            }

            try
            {
                var handler = _handlerResolver.GetHandler(rawMessage.Op);
                await handler.HandleAsync(webSocket, rawMessage.D);
            }
            catch (NotSupportedException e)
            {
                await SendErrorAsync(webSocket, ErrorCodes.Unsupported, e.Message);
            }
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
