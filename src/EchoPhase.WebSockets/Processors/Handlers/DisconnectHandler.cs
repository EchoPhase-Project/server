using System.Net.WebSockets;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors.Payloads;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    [OpCodeHandler(OpCodes.Disconnect)]
    public class DisconnectHandler : OpCodeHandlerBase<DisconnectPayload>
    {
        private readonly WebSocketService _webSocketService;
        private readonly WebSocketConnectionManager _connectionManager;

        public DisconnectHandler(IServiceProvider serviceProvider)
        : base(serviceProvider)
        {
            _webSocketService = GetService<WebSocketService>();
            _connectionManager = GetService<WebSocketConnectionManager>();
        }

        public override async Task HandleAsync(WebSocket webSocket, DisconnectPayload payload)
        {
            var userId = await _connectionManager.GetUserIdAsync(webSocket);
            if (userId == Guid.Empty)
                return;

            var response = EventMessage.Create(OpCodes.DisconnectAck);

            await _webSocketService.SendMessageAsync(webSocket, response);
            await _connectionManager.CloseConnectionAsync(userId, webSocket);
        }
    }
}

