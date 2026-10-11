// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using System.Security.Claims;
using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.Security.Authentication.Jwt.Verdicts;
using EchoPhase.WebSockets;
using Microsoft.Extensions.Options;
using WebSocketOptions = EchoPhase.Configuration.WebSocket.WebSocketOptions;

namespace EchoPhase.Server.Services.Realtime
{
    /// <summary>
    /// Authentication only runs when a realtime connection opens. This
    /// periodically re-checks every open gateway connection and
    /// drops those whose token has expired or been revoked (security stamp
    /// change, deleted API client, ended impersonation).
    /// </summary>
    public class RealtimeCredentialMonitor : BackgroundService
    {
        private const string RevokedReason = "Credentials expired or revoked";

        private readonly IServiceScopeFactory _scopes;
        private readonly WebSocketConnectionManager _gateway;
        private readonly WebSocketOptions _settings;
        private readonly ILogger<RealtimeCredentialMonitor> _logger;

        public RealtimeCredentialMonitor(
            IServiceScopeFactory scopes,
            WebSocketConnectionManager gateway,
            IOptions<WebSocketOptions> settings,
            ILogger<RealtimeCredentialMonitor> logger)
        {
            _scopes = scopes;
            _gateway = gateway;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_settings.RevalidationInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RevalidateAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One failed run (e.g. database down) must not stop monitoring,
                    // and must not disconnect everyone either.
                    _logger.LogError(ex, "Realtime credential revalidation failed.");
                }
            }
        }

        private async Task RevalidateAsync(CancellationToken ct)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenProvider>();

            // Users often hold several connections with the same token; check each token once.
            var verdicts = new Dictionary<string, bool>();

            async Task<bool> IsValidAsync(ClaimsPrincipal principal)
            {
                var key = PrincipalVerdictKey.For(principal);
                if (!verdicts.TryGetValue(key, out var valid))
                    verdicts[key] = valid = await jwt.IsPrincipalValidAsync(principal, ct);
                return valid;
            }

            foreach (var connection in _gateway.GetAllConnections().ToList())
            {
                if (await IsValidAsync(connection.User))
                    continue;

                _logger.LogInformation("Closing gateway connection {ConnectionId} of user {UserId}: {Reason}",
                    connection.Id, connection.UserId, RevokedReason);

                await _gateway.CloseConnectionAsync(connection, WebSocketCloseStatus.PolicyViolation, RevokedReason);
            }
        }
    }
}
