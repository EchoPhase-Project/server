using EchoPhase.Interfaces;
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

        public async Task SendMessageToAllAsync<T, TS>(T message, ISet<string> intents, TS shardId)
            where TS : struct
        {
            await _webSocketService.SendMessageToAllAsync(message, intents, shardId);
            await _webHookService.SendMessageToAllAsync(message, intents);
        }

        public async Task SendMessageToUsersAsync<T, TS>(ISet<Guid> userIds, T message, ISet<string> intents, TS shardId)
            where TS : struct
        {
            await _webSocketService.SendMessageToUsersAsync(userIds, message, intents, shardId);
            await _webHookService.SendMessageToUsersAsync(userIds, message, intents);
        }

        public async Task SendMessageToRolesAsync<T, TS>(ISet<string> roles, T message, ISet<string> intents, TS shardId)
            where TS : struct
        {
            await _webSocketService.SendMessageToRolesAsync(roles, message, intents, shardId);
            await _webHookService.SendMessageToRolesAsync(roles, message, intents);
        }

        public async Task SendMessageToAllAsync<T>(T message, ISet<string> intents)
        {
            await _webSocketService.SendMessageToAllAsync(message, intents);
            await _webHookService.SendMessageToAllAsync(message, intents);
        }

        public async Task SendMessageToUsersAsync<T>(ISet<Guid> userIds, T message, ISet<string> intents)
        {
            await _webSocketService.SendMessageToUsersAsync(userIds, message, intents);
            await _webHookService.SendMessageToUsersAsync(userIds, message, intents);
        }

        public async Task SendMessageToRolesAsync<T>(ISet<string> roles, T message, ISet<string> intents)
        {
            await _webSocketService.SendMessageToRolesAsync(roles, message, intents);
            await _webHookService.SendMessageToRolesAsync(roles, message, intents);
        }
    }
}
