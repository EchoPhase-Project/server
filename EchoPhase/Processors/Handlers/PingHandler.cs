using System.Net.WebSockets;
using EchoPhase.Attributes;
using EchoPhase.Processors.Enums;
using EchoPhase.Processors.Payloads;
using EchoPhase.Services.WebSockets;

namespace EchoPhase.Processors.Handlers
{
    [OpCodeHandler(OpCodes.Ping)]
    public class PingHandler : OpCodeHandlerBase<PingPayload>
    {
        private readonly WebSocketService _webSocketService;
        private readonly WebSocketConnectionManager _connectionManager;

        public PingHandler(IServiceProvider serviceProvider)
        : base(serviceProvider)
        {
            _webSocketService = GetService<WebSocketService>();
            _connectionManager = GetService<WebSocketConnectionManager>();
        }

        public override async Task HandleAsync(WebSocket webSocket, PingPayload payload)
        {
            await _connectionManager.RefreshConnectionAsync(webSocket);

            var response = EventMessage.Create(OpCodes.Pong);

            await _webSocketService.SendMessageToConnectionAsync(webSocket, response);
        }
    }
}
