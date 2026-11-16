// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.WebSockets.Cluster;

namespace EchoPhase.Server.Services.Realtime
{
    /// <summary>
    /// Closes the gateway connections of ended sessions on every replica,
    /// at once. If the backplane cannot take the order, the credential
    /// monitor closes them on its next pass.
    /// </summary>
    public class SessionEventsNotifier : ISessionEvents
    {
        private readonly GatewayDispatcher _dispatcher;
        private readonly ILogger<SessionEventsNotifier> _logger;

        public SessionEventsNotifier(GatewayDispatcher dispatcher, ILogger<SessionEventsNotifier> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;
        }

        public async Task EndedAsync(Guid userId, IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default)
        {
            try
            {
                await _dispatcher.CloseSessionsAsync(userId, sessionIds, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // The sign-out itself succeeded; it must not fail over this.
                _logger.LogWarning(e, "Could not close the gateway connections of {Count} ended session(s) of user {UserId}",
                    sessionIds.Count, userId);
            }
        }
    }
}
