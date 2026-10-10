// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net.WebSockets;
using System.Security.Claims;
using EchoPhase.Configuration.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EchoPhase.WebSockets
{
    /// <summary>
    /// The open gateway sockets of this instance, by user. Lookups are lock-free:
    /// each user's connections are an immutable array swapped on add and
    /// remove, so senders iterate a snapshot while sockets come and go.
    /// </summary>
    public class WebSocketConnectionManager
    {
        private readonly WebSocketOptions _settings;
        private readonly TimeProvider _time;
        private readonly ILogger<WebSocketConnectionManager> _logger;

        private readonly ConcurrentDictionary<Guid, ImmutableArray<WebSocketConnection>> _byUser = new();
        private readonly ConcurrentDictionary<WebSocket, WebSocketConnection> _bySocket = new();

        public WebSocketConnectionManager(
            ILogger<WebSocketConnectionManager> logger,
            IOptions<WebSocketOptions> settings,
            IHostApplicationLifetime lifetime,
            TimeProvider time)
        {
            _logger = logger;
            _settings = settings.Value;
            _time = time;

            lifetime.ApplicationStopping.Register(() => CloseAllAsync(
                WebSocketCloseStatus.EndpointUnavailable, "Server shutting down").GetAwaiter().GetResult());
        }

        public WebSocketConnection Add(Guid userId, WebSocket webSocket, ClaimsPrincipal user)
        {
            var connection = new WebSocketConnection(userId, webSocket, user, _time.GetTimestamp());
            _bySocket[webSocket] = connection;
            _byUser.AddOrUpdate(userId, [connection], (_, list) => list.Add(connection));

            _logger.LogInformation("WebSocket connection added. UserId: {UserId}, ConnectionId: {ConnectionId}",
                userId, connection.Id);
            return connection;
        }

        /// <summary>Forgets a connection (the receive loop calls this when its socket ends).</summary>
        public void Remove(WebSocketConnection connection)
        {
            _bySocket.TryRemove(connection.WebSocket, out _);

            while (_byUser.TryGetValue(connection.UserId, out var list))
            {
                var updated = list.Remove(connection);
                if (updated.IsEmpty
                        ? _byUser.TryRemove(new KeyValuePair<Guid, ImmutableArray<WebSocketConnection>>(connection.UserId, list))
                        : _byUser.TryUpdate(connection.UserId, updated, list))
                    break;
            }

            connection.Dispose();
        }

        public IReadOnlyList<WebSocketConnection> GetConnections(Guid userId)
            => _byUser.TryGetValue(userId, out var list) ? list : [];

        public bool TryGetConnection(WebSocket webSocket, out WebSocketConnection connection)
            => _bySocket.TryGetValue(webSocket, out connection!);

        public IEnumerable<WebSocketConnection> GetAllConnections() => _bySocket.Values;

        public IEnumerable<Guid> GetConnectedUserIds() => _byUser.Keys;

        public int GetConnectionCount(Guid userId) => GetConnections(userId).Count;

        public int GetTotalConnectionCount() => _bySocket.Count;

        public void Touch(WebSocketConnection connection) => connection.Touch(_time.GetTimestamp());

        public Task CloseConnectionAsync(WebSocketConnection connection, WebSocketCloseStatus status, string description)
            => connection.CloseAsync(status, description, _settings.CloseTimeout);

        public Task CloseConnectionsAsync(Guid userId, WebSocketCloseStatus status, string description)
            => Task.WhenAll(GetConnections(userId).Select(c => CloseConnectionAsync(c, status, description)));

        public Task CloseAllAsync(WebSocketCloseStatus status, string description)
            => Task.WhenAll(_bySocket.Values.Select(c => CloseConnectionAsync(c, status, description)));

        /// <summary>
        /// Closes connections that sent nothing for a heartbeat interval (the
        /// client stopped pinging); returns how many.
        /// </summary>
        public async Task<int> CloseSilentAsync()
        {
            var now = _time.GetTimestamp();
            var silent = _bySocket.Values
                .Where(c => _time.GetElapsedTime(c.LastSeen, now) > _settings.HeartbeatInterval)
                .ToList();

            foreach (var connection in silent)
            {
                _logger.LogInformation("Heartbeat timeout for connection {ConnectionId}, closing", connection.Id);
                await CloseConnectionAsync(connection, WebSocketCloseStatus.PolicyViolation, "Heartbeat timeout");
            }

            return silent.Count;
        }
    }
}
