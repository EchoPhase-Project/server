// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Discord.WebSocket;
using EchoPhase.Connections;
using EchoPhase.Connections.Services;
using EchoPhase.Interfaces.Services;
using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Services.Connections
{
    /// <summary>
    /// What happens when a Discord gateway connection stops for good. A
    /// rejected token (4004) goes through connection recovery, which marks a
    /// manual token as needing a new one and tells the owner. Anything else
    /// (intents not enabled for the bot, sharding, ...) is told to the owner as
    /// a <c>discord_gateway.failed</c> event (intent connections) with what to fix.
    /// </summary>
    public sealed class DiscordGatewayErrorHandler : IHostedService, IDiscordGatewayObserver
    {
        private readonly DiscordWebSocketManager _gateway;
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<DiscordGatewayErrorHandler> _logger;

        public DiscordGatewayErrorHandler(
            DiscordWebSocketManager gateway, IServiceScopeFactory scopes, ILogger<DiscordGatewayErrorHandler> logger)
        {
            _gateway = gateway;
            _scopes = scopes;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _gateway.AddObserver(this);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task OnFailedAsync(DiscordGatewayFailure failure, CancellationToken ct = default)
        {
            await using var scope = _scopes.CreateAsyncScope();

            if (failure.AuthenticationFailed)
            {
                var connections = scope.ServiceProvider.GetRequiredService<IConnectionService>();
                var reference = new ConnectionRef(failure.UserId, failure.TokenName);
                var current = await connections.ResolveAsync(reference, ConnectionProviders.Discord, ct);
                if (current.Successful)
                    await connections.RecoverAsync(reference, ConnectionProviders.Discord, current.Value!.AccessToken, ct);
                return;
            }

            var events = scope.ServiceProvider.GetRequiredService<IEventService>();
            await events.SendMessageToUsersAsync(
                [failure.UserId],
                new
                {
                    type = "discord_gateway.failed",
                    gateway = new
                    {
                        connectionId = failure.ConnectionId,
                        connection = failure.TokenName,
                        closeCode = failure.CloseCode,
                        message = failure.Message
                    }
                },
                [Intents.Connections],
                ct);

            _logger.LogWarning("Told user {UserId} that Discord gateway {Connection} stopped: {Message}",
                failure.UserId, failure.TokenName, failure.Message);
        }
    }
}
