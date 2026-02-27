using EchoPhase.Interfaces;
using EchoPhase.WebHooks;
using EchoPhase.WebSockets;

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

        public async Task SendMessageToAllAsync<T, TS>(T message, HashSet<string> intents, TS shardId)
            where TS : struct
        {
            await _webSocketService.BroadcastMessageAsync(message, intents, shardId);
            await _webHookService.SendMessageToAllAsync(message, intents);
        }

        public async Task SendMessageToUsersAsync<T, TS>(HashSet<Guid> userIds, T message, HashSet<string> intents, TS shardId)
            where TS : struct
        {
            await _webSocketService.SendMessageToUsersAsync(userIds, message, intents, shardId);
            await _webHookService.SendMessageToUsersAsync(userIds, message, intents);
        }

        public async Task SendMessageToRolesAsync<T, TS>(HashSet<string> roles, T message, HashSet<string> intents, TS shardId)
            where TS : struct
        {
            await _webSocketService.SendMessageToRolesAsync(roles, message, intents, shardId);
            await _webHookService.SendMessageToRolesAsync(roles, message, intents);
        }

        public async Task SendMessageToAllAsync<T>(T message, HashSet<string> intents)
        {
            await _webSocketService.BroadcastMessageAsync(message, intents);
            await _webHookService.SendMessageToAllAsync(message, intents);
        }

        public async Task SendMessageToUsersAsync<T>(HashSet<Guid> userIds, T message, HashSet<string> intents)
        {
            await _webSocketService.SendMessageToUsersAsync(userIds, message, intents);
            await _webHookService.SendMessageToUsersAsync(userIds, message, intents);
        }

        public async Task SendMessageToRolesAsync<T>(HashSet<string> roles, T message, HashSet<string> intents)
        {
            await _webSocketService.SendMessageToRolesAsync(roles, message, intents);
            await _webHookService.SendMessageToRolesAsync(roles, message, intents);
        }
    }
}
