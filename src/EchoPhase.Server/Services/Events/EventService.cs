// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Server.Interfaces.Services;
using EchoPhase.WebHooks;
using EchoPhase.WebSockets;

namespace EchoPhase.Server.Services.Events
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

        public async Task SendMessageToAllAsync<T>(T message, HashSet<string> intents, CancellationToken ct = default,
            string? shardKey = null)
        {
            await _webSocketService.BroadcastMessageAsync(message, intents, ct, shardKey);
            await _webHookService.SendMessageToAllAsync(message, intents, ct: ct);
        }

        public async Task SendMessageToUsersAsync<T>(HashSet<Guid> userIds, T message, HashSet<string> intents,
            CancellationToken ct = default, string? shardKey = null)
        {
            await _webSocketService.SendMessageToUsersAsync(userIds, message, intents, ct, shardKey);
            await _webHookService.SendMessageToUsersAsync(userIds, message, intents, ct: ct);
        }

        public async Task SendMessageToRolesAsync<T>(HashSet<string> roles, T message, HashSet<string> intents,
            CancellationToken ct = default, string? shardKey = null)
        {
            await _webSocketService.SendMessageToRolesAsync(roles, message, intents, ct, shardKey);
            await _webHookService.SendMessageToRolesAsync(roles, message, intents, ct: ct);
        }
    }
}
