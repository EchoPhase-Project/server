// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Clients.Discord.WebSocket
{
    public sealed class DiscordEventListener : IDiscordWebSocketSubscriber
    {
        private readonly ILogger<DiscordEventListener> _logger;
        private DiscordWebSocketSubscription? _subscription;
        private bool _disposed;

        public DiscordEventListener(ILogger<DiscordEventListener> logger)
        {
            _logger = logger;
        }

        public void Register(DiscordWebSocketManager manager, Guid userId)
        {
            _subscription = manager.Subscribe(userId, this);
        }

        public Task OnPayloadAsync(
            Guid userId,
            string tokenName,
            Guid connectionId,
            JsonDocument payload)
        {
            var op = payload.RootElement.GetProperty("op").GetInt32();

            if (op == 0 && payload.RootElement.TryGetProperty("t", out var t))
            {
                _logger.LogInformation(
                    "[{TokenName}] Event={Event} ConnectionId={ConnectionId}",
                    tokenName, t.GetString(), connectionId);

                var eventName = t.GetString();
                return eventName switch
                {
                    "MESSAGE_CREATE" => HandleMessageCreate(userId, payload),
                    "GUILD_MEMBER_ADD" => HandleGuildMemberAdd(userId, payload),
                    _ => Task.CompletedTask
                };
            }

            return Task.CompletedTask;
        }

        private Task HandleMessageCreate(Guid userId, JsonDocument payload)
        {
            var content = payload.RootElement
                .GetProperty("d")
                .GetProperty("content")
                .GetString();

            _logger.LogInformation("New message for user {UserId}: {Content}", userId, content);
            return Task.CompletedTask;
        }

        private Task HandleGuildMemberAdd(Guid userId, JsonDocument payload)
        {
            _logger.LogInformation("New guild member for user {UserId}", userId);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _subscription?.Dispose();
        }
    }
}
