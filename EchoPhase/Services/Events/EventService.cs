using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;
using EchoPhase.Services.WebHooks;
using EchoPhase.Services.WebSockets;

namespace EchoPhase.Services.Events
{
	public class EventService : IEventService
	{
		private readonly WebSocketService _webSocketService;
		private readonly WebHookService _webHookService;

		public EventService(
				WebSocketService webSocketService,
				WebHookService webHookService)
		{
			_webSocketService = webSocketService;
			_webHookService = webHookService;
		}

		public async Task SendMessageToAllAsync<T>(T message, IntentsFlags intents, Guid? shardId = null)
		{
			await _webSocketService.SendMessageToAllAsync(message, intents, shardId);
			await _webHookService.SendMessageToAllAsync(message, intents);
		}

		public async Task SendMessageToUserAsync<T>(Guid userId, T message, IntentsFlags intents, Guid? shardId = null)
		{
			await _webSocketService.SendMessageToUserAsync(userId, message, intents, shardId);
			await _webHookService.SendMessageToUserAsync(userId, message, intents);
		}

		public async Task SendMessageToRoleAsync<T>(string role, T message, IntentsFlags intents, Guid? shardId = null)
		{
			await _webSocketService.SendMessageToRoleAsync(role, message, intents, shardId);
			await _webHookService.SendMessageToRoleAsync(role, message, intents);
		}
	}
}
