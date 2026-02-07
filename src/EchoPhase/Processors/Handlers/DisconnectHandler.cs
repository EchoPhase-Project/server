using System.Net.WebSockets;
using EchoPhase.Attributes;
using EchoPhase.Processors.Enums;
using EchoPhase.Processors.Payloads;
using EchoPhase.Services.WebSockets;

namespace EchoPhase.Processors.Handlers
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

            await _webSocketService.SendMessageToConnectionAsync(webSocket, response);
            await _connectionManager.CloseConnectionAsync(userId, webSocket);
        }
    }
}

