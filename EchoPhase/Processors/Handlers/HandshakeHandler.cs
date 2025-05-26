using System.Net.WebSockets;

using EchoPhase.Models;
using EchoPhase.Attributes;
using EchoPhase.Exceptions;
using EchoPhase.Processors.Enums;
using EchoPhase.Processors.Payloads;
using EchoPhase.Services.WebSockets;

namespace EchoPhase.Processors.Handlers
{
	[OpCodeHandler(OpCodes.Handshake)]
	public class HandshakeHandler : OpCodeHandlerBase<HandshakePayload>
	{
		private readonly WebSocketService _webSocketService;
		private readonly WebSocketConnectionManager _connectionManager;

		public HandshakeHandler(IServiceProvider serviceProvider) 
        : base(serviceProvider)
		{
			_webSocketService = GetService<WebSocketService>();
			_connectionManager = GetService<WebSocketConnectionManager>();
		}

		public override async Task HandleAsync(WebSocket webSocket, HandshakePayload payload)
		{
			try
			{
				var connection = await _connectionManager.GetConnectionAsync(webSocket);
				connection.Intents = payload.Intents.HasValue ? payload.Intents.Value : 0;

				var response = new EventMessage
				{
					Op = OpCodes.HandshakeAck,
					D = new HandshakeAckPayload()
					{
						HeartbeatInterval = WebSocketConnectionManager.heartbeatInterval.TotalMilliseconds
					}
				};

				await _webSocketService.SendMessageToConnectionAsync(webSocket, response);
			}
			catch(WebSocketConnectionNotFoundException)
			{
				string errorMessage = "Connection not found.";

				var response = new EventMessage()
				{
					Op = OpCodes.Error,
					D = new ErrorPayload()
					{
						Code = ErrorCodes.NotFound,
						Message = errorMessage
					}
				};

				await _webSocketService.SendMessageToConnectionAsync(webSocket, response);

				throw;
			}

			return;
		}
	}
}
