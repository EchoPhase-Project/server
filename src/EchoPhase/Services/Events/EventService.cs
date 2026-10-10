// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Interfaces.Services;
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

        public async Task SendMessageToAllAsync<T, TS>(T message, HashSet<string> intents, TS shardId, CancellationToken ct = default)
            where TS : struct
        {
            await _webSocketService.BroadcastMessageAsync(message, intents, shardId, ct);
            await _webHookService.SendMessageToAllAsync(message, intents, ct: ct);
        }

        public async Task SendMessageToUsersAsync<T, TS>(HashSet<Guid> userIds, T message, HashSet<string> intents, TS shardId, CancellationToken ct = default)
            where TS : struct
        {
            await _webSocketService.SendMessageToUsersAsync(userIds, message, intents, shardId, ct);
            await _webHookService.SendMessageToUsersAsync(userIds, message, intents, ct: ct);
        }

        public async Task SendMessageToRolesAsync<T, TS>(HashSet<string> roles, T message, HashSet<string> intents, TS shardId, CancellationToken ct = default)
            where TS : struct
        {
            await _webSocketService.SendMessageToRolesAsync(roles, message, intents, shardId, ct);
            await _webHookService.SendMessageToRolesAsync(roles, message, intents, ct: ct);
        }

        public async Task SendMessageToAllAsync<T>(T message, HashSet<string> intents, CancellationToken ct = default)
        {
            await _webSocketService.BroadcastMessageAsync(message, intents, ct);
            await _webHookService.SendMessageToAllAsync(message, intents, ct: ct);
        }

        public async Task SendMessageToUsersAsync<T>(HashSet<Guid> userIds, T message, HashSet<string> intents, CancellationToken ct = default)
        {
            await _webSocketService.SendMessageToUsersAsync(userIds, message, intents, ct);
            await _webHookService.SendMessageToUsersAsync(userIds, message, intents, ct: ct);
        }

        public async Task SendMessageToRolesAsync<T>(HashSet<string> roles, T message, HashSet<string> intents, CancellationToken ct = default)
        {
            await _webSocketService.SendMessageToRolesAsync(roles, message, intents, ct);
            await _webHookService.SendMessageToRolesAsync(roles, message, intents, ct: ct);
        }
    }
}
