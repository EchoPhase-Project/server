using System.Text.Json;
using System.Net.WebSockets;

using EchoPhase.Models;
using EchoPhase.Processors.Enums;
using EchoPhase.Processors.Handlers;
using EchoPhase.Processors.Payloads;
using EchoPhase.Services.WebSockets;
using EchoPhase.Interfaces;

namespace EchoPhase.Processors
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
			EventMessage? eventMessage = JsonSerializer.Deserialize<EventMessage>(message);
			if (eventMessage == null)
			{
				string errorMessage = "Unable to deserialize JSON.";

				var response = new EventMessage()
				{
					Op = OpCodes.Error,
					D = new ErrorPayload()
					{
						Code = ErrorCodes.DeserializationError,
						Message = errorMessage
					}
				};

				await _webSocketService.SendMessageToConnectionAsync(webSocket, response);

				throw new InvalidOperationException(errorMessage);
			}

			await HandleMessageAsync(webSocket, eventMessage);
		}


		public async Task HandleMessageAsync(WebSocket webSocket, EventMessage eventMessage)
		{
			try
			{
				var handler = _handlerResolver.GetHandler(eventMessage.Op);

				await handler.HandleAsync(webSocket, eventMessage.D);
			}
			catch(NotSupportedException e)
			{
				var errorMessage = new EventMessage
				{
					Op = OpCodes.Error,
					D = new ErrorPayload()
					{
						Code = ErrorCodes.Unsupported,
						Message = e.Message
					}
				};

				await _webSocketService.SendMessageToConnectionAsync(webSocket, errorMessage);
			}
		}
	}
}
