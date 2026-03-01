using System.Collections.Concurrent;
using System.Net.WebSockets;
using EchoPhase.Configuration.WebSocket;
using EchoPhase.DAL.Redis.Interfaces;
using EchoPhase.DAL.Redis.Models;
using EchoPhase.WebSockets.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EchoPhase.WebSockets
{
    public class WebSocketConnectionManager
    {
        private readonly WebSocketOptions _settings;

        private readonly ConcurrentDictionary<Guid, List<WebSocketConnection>> _connections = new();
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        private readonly ICacheContext _cacheContext;
        private readonly ILogger<WebSocketConnectionManager> _logger;

        public WebSocketConnectionManager(
            ICacheContext cacheContext,
            ILogger<WebSocketConnectionManager> logger,
            IOptions<WebSocketOptions> settings)
        {
            _cacheContext = cacheContext;
            _logger = logger;
            _settings = settings.Value;
        }

        public async Task AddConnectionAsync(Guid userId, WebSocket webSocket, HttpContext context)
        {
            var connection = new WebSocketConnection
            {
                WebSocket = webSocket,
                HttpContext = context
            };

            await _connectionLock.WaitAsync();
            try
            {
                _connections.AddOrUpdate(userId,
                    new List<WebSocketConnection> { connection },
                    (key, existingConnections) =>
                    {
                        existingConnections.Add(connection);
                        return existingConnections;
                    });

                await _cacheContext
                    .Entry<WebSocketCache>(connection.Id.ToString())
                    .SetAsync(new WebSocketCache { UserId = userId }, TimeSpan.FromMinutes(10));
            }
            finally
            {
                _connectionLock.Release();
            }

            _logger.LogInformation("WebSocket connection added. UserId: {UserId}, ConnectionId: {ConnectionId}",
                userId, connection.Id);

            StartHeartbeatTask(userId, connection);
        }

        public bool TryGetConnections(Guid userId, out List<WebSocketConnection>? connections)
        {
            return _connections.TryGetValue(userId, out connections);
        }

        public List<WebSocketConnection> GetConnections(Guid userId)
        {
            if (!_connections.TryGetValue(userId, out var connections))
                throw new WebSocketConnectionNotFoundException(userId);

            return connections;
        }

        public WebSocketConnection GetConnection(Guid userId, WebSocket webSocket)
        {
            if (!_connections.TryGetValue(userId, out var connections))
                throw new WebSocketConnectionNotFoundException(userId);

            var connection = connections.FirstOrDefault(conn => conn.WebSocket == webSocket);
            return connection ?? throw new WebSocketConnectionNotFoundException(userId, webSocket);
        }

        public async Task<WebSocketConnection> GetConnectionAsync(WebSocket webSocket)
        {
            var userId = await GetUserIdAsync(webSocket);

            if (userId == Guid.Empty)
                throw new WebSocketConnectionNotFoundException(userId);

            if (!_connections.TryGetValue(userId, out var connections))
                throw new WebSocketConnectionNotFoundException(userId);

            var connection = connections.FirstOrDefault(conn => conn.WebSocket == webSocket);
            return connection ?? throw new WebSocketConnectionNotFoundException(userId, webSocket);
        }

        public IEnumerable<(Guid UserId, WebSocketConnection Connection)> GetAllConnections()
        {
            return _connections.SelectMany(
                pair => pair.Value,
                (pair, connection) => (pair.Key, connection));
        }

        public IEnumerable<Guid> GetConnectedUserIds()
        {
            return _connections.Keys;
        }

        public int GetConnectionCount(Guid userId)
        {
            return _connections.TryGetValue(userId, out var connections)
                ? connections.Count
                : 0;
        }

        public int GetTotalConnectionCount()
        {
            return _connections.Values.Sum(c => c.Count);
        }

        public async Task CloseConnectionAsync(WebSocketConnection connection)
        {
            using var cts = new CancellationTokenSource(_settings.CloseTimeout);

            try
            {
                if (connection.WebSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    try
                    {
                        await connection.WebSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Connection closed by server",
                            cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Close timeout exceeded for connection {ConnectionId}, aborting",
                            connection.Id);
                        connection.WebSocket.Abort();
                    }
                }
                else
                {
                    connection.WebSocket.Abort();
                }

                await _cacheContext
                    .Entry<WebSocketCache>(connection.Id.ToString())
                    .RemoveAsync();
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket error while closing connection {ConnectionId}",
                    connection.Id);
            }
            finally
            {
                await RemoveConnectionAsync(connection);
            }
        }

        private async Task RemoveConnectionAsync(WebSocketConnection connection)
        {
            await _connectionLock.WaitAsync();
            try
            {
                var userId = await GetUserIdAsync(connection);

                if (_connections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(connection);

                    if (connections.Count == 0)
                    {
                        _connections.TryRemove(userId, out _);
                        _logger.LogInformation("All connections closed for UserId: {UserId}", userId);
                    }
                }

                connection.Dispose();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task CloseConnectionAsync(Guid userId, WebSocket webSocket)
        {
            if (!_connections.TryGetValue(userId, out var connections))
                return;

            var connection = connections.FirstOrDefault(conn => conn.WebSocket == webSocket);
            if (connection is not null)
                await CloseConnectionAsync(connection);
        }

        public async Task CloseConnectionsAsync(Guid userId)
        {
            if (!_connections.TryGetValue(userId, out var connections))
                return;

            var connectionsToClose = connections.ToList();
            await CloseConnectionsInternalAsync(connectionsToClose);
        }

        public async Task CloseAllConnectionsAsync()
        {
            var allConnections = _connections.Values
                .SelectMany(c => c)
                .ToList();

            await CloseConnectionsInternalAsync(allConnections);
        }

        private async Task CloseConnectionsInternalAsync(IEnumerable<WebSocketConnection> connections)
        {
            var tasks = connections.Select(CloseConnectionAsync);
            await Task.WhenAll(tasks);
        }

        public void AbortAllConnections()
        {
            _logger.LogWarning("Aborting all WebSocket connections");

            foreach (var connections in _connections.Values)
            {
                foreach (var connection in connections)
                {
                    try
                    {
                        connection.WebSocket.Abort();
                        connection.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error aborting connection {ConnectionId}", connection.Id);
                    }
                }
            }

            _connections.Clear();
        }

        private void StartHeartbeatTask(Guid userId, WebSocketConnection connection)
        {
            connection.HeartbeatCancellationTokenSource?.Cancel();
            connection.HeartbeatCancellationTokenSource?.Dispose();
            connection.HeartbeatCancellationTokenSource = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_settings.HeartbeatInterval, connection.HeartbeatCancellationTokenSource.Token);

                    _logger.LogInformation(
                        "Heartbeat timeout for connection {ConnectionId}, closing",
                        connection.Id);

                    await CloseConnectionAsync(connection);
                }
                catch (TaskCanceledException)
                {
                    // Normal cancellation, heartbeat was refreshed
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in heartbeat task for connection {ConnectionId}",
                        connection.Id);
                }
            }, connection.HeartbeatCancellationTokenSource.Token);
        }

        public void RefreshHeartbeat(Guid userId, WebSocketConnection connection)
        {
            StartHeartbeatTask(userId, connection);
        }

        public async Task RefreshHeartbeatAsync(WebSocket webSocket)
        {
            var userId = await GetUserIdAsync(webSocket);
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Cannot refresh heartbeat: UserId not found for WebSocket");
                return;
            }

            if (!_connections.TryGetValue(userId, out var connections))
            {
                _logger.LogWarning("Cannot refresh heartbeat: No connections for UserId {UserId}", userId);
                return;
            }

            var connection = connections.FirstOrDefault(c => c.WebSocket == webSocket);
            if (connection is null)
            {
                _logger.LogWarning("Cannot refresh heartbeat: Connection not found");
                return;
            }

            RefreshHeartbeat(userId, connection);
        }

        public async Task<Guid> GetUserIdAsync(WebSocket webSocket)
        {
            var cacheKey = webSocket.GetHashCode().ToString();

            var webSocketCache = await _cacheContext
                .Entry<WebSocketCache>(cacheKey)
                .GetOrSetAsync(
                    () => Task.FromResult(FindUserIdByWebSocket(webSocket)),
                    TimeSpan.FromMinutes(10));

            return webSocketCache.UserId;
        }

        public async Task<Guid> GetUserIdAsync(WebSocketConnection connection)
        {
            var cacheKey = connection.Id.ToString();

            var webSocketCache = await _cacheContext
                .Entry<WebSocketCache>(cacheKey)
                .GetOrSetAsync(
                    () => Task.FromResult(FindUserIdByConnection(connection)),
                    TimeSpan.FromMinutes(10));

            return webSocketCache.UserId;
        }

        private WebSocketCache FindUserIdByConnection(WebSocketConnection connection)
        {
            foreach (var (userId, connections) in _connections)
            {
                if (connections.Contains(connection))
                    return new WebSocketCache { UserId = userId };
            }

            return new WebSocketCache { UserId = Guid.Empty };
        }

        private WebSocketCache FindUserIdByWebSocket(WebSocket webSocket)
        {
            foreach (var (userId, connections) in _connections)
            {
                if (connections.Any(conn => conn.WebSocket == webSocket))
                    return new WebSocketCache { UserId = userId };
            }

            return new WebSocketCache { UserId = Guid.Empty };
        }
    }
}
