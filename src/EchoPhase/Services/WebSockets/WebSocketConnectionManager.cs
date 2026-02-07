using System.Collections.Concurrent;
using System.Net.WebSockets;
using EchoPhase.DAL.Redis.Interfaces;
using EchoPhase.DAL.Redis.Models;
using EchoPhase.Exceptions;
using EchoPhase.Services.WebSockets.Models;

namespace EchoPhase.Services.WebSockets
{
    public class WebSocketConnectionManager
    {
        public static readonly TimeSpan closeTimeout = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan heartbeatInterval = TimeSpan.FromSeconds(45);

        private static readonly ConcurrentDictionary<Guid, List<WebSocketConnection>> _connections = new();
        private static readonly ConcurrentQueue<string> _removalQueue = new();

        private readonly ICacheContext _cacheContext;
        private readonly ILogger<WebSocketConnectionManager> _logger;

        public WebSocketConnectionManager(
            ICacheContext CacheContext,
            ILogger<WebSocketConnectionManager> logger)
        {
            _cacheContext = CacheContext;
            _logger = logger;
        }

        public void AddConnection(Guid userId, WebSocket webSocket, HttpContext context)
        {
            var connection = new WebSocketConnection
            {
                WebSocket = webSocket,
                HttpContext = context
            };

            _connections.AddOrUpdate(userId,
                new List<WebSocketConnection> { connection },
                (key, existingConnections) =>
                {
                    existingConnections.Add(connection);
                    return existingConnections;
                });

            StartHeartbeatTask(userId, connection);
        }

        public List<WebSocketConnection> GetConnections(Guid userId)
        {
            _connections.TryGetValue(userId, out var connections);
            return connections ??
                throw new WebSocketConnectionNotFoundException(userId);
        }

        public WebSocketConnection GetConnection(Guid userId, WebSocket webSocket)
        {
            if (_connections.TryGetValue(userId, out var connections))
            {
                return connections.FirstOrDefault(conn => conn.WebSocket == webSocket) ??
                    throw new WebSocketConnectionNotFoundException(userId, webSocket);
            }
            throw new WebSocketConnectionNotFoundException(userId);
        }

        public async Task<WebSocketConnection> GetConnectionAsync(WebSocket webSocket)
        {
            var userId = await GetUserIdAsync(webSocket);

            if (userId == Guid.Empty)
                throw new WebSocketConnectionNotFoundException(userId);

            var connections = _connections[userId];

            var connection = connections.FirstOrDefault(conn => conn.WebSocket == webSocket);
            return connection ??
                throw new WebSocketConnectionNotFoundException(userId, webSocket);
        }

        public IEnumerable<(Guid Key, WebSocketConnection Value)> GetConnections()
        {
            return _connections.SelectMany(pair => pair.Value, (pair, connection) => (pair.Key, connection));
        }

        public IEnumerable<Guid> GetUserIds()
        {
            return _connections.Select(pair => pair.Key).Distinct();
        }

        public async Task CloseConnectionAsync(WebSocketConnection connection)
        {
            var cts = new CancellationTokenSource(closeTimeout);
            try
            {
                if (connection.WebSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    var closeTask = connection.WebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed by server",
                        cts.Token
                    );

                    var delayTask = Task.Delay(Timeout.Infinite, cts.Token);
                    await Task.WhenAny(closeTask, delayTask);

                    if (!closeTask.IsCompleted)
                        connection.WebSocket.Abort();
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
                _logger.LogWarning(ex, $"WebSocket error while closing connection with id {connection.Id}.");
            }
            finally
            {
                connection.Dispose();

                var userId = await GetUserIdAsync(connection);
                if (_connections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(connection);
                    if (connections.Count == 0)
                        _connections.TryRemove(userId, out _);
                }
            }
        }

        public async Task CloseConnectionAsync(Guid userId, WebSocket webSocket)
        {
            if (_connections.TryGetValue(userId, out var connections))
            {
                var connection = connections.FirstOrDefault(conn => conn.WebSocket == webSocket);
                if (connection is not null)
                    await CloseConnectionAsync(connection);
            }
        }

        public Task CloseConnectionsAsync(Guid userId)
        {
            if (_connections.TryGetValue(userId, out var connections))
                return CloseConnectionsInternalAsync(connections);

            return Task.CompletedTask;
        }

        public Task CloseConnectionsAsync()
        {
            var allConnections = _connections.Values.SelectMany(c => c);
            return CloseConnectionsInternalAsync(allConnections);
        }

        private async Task CloseConnectionsInternalAsync(IEnumerable<WebSocketConnection> connections)
        {
            var tasks = connections.Select(CloseConnectionAsync);
            await Task.WhenAll(tasks);
        }

        public void AbortConnections()
        {
            foreach (var (userId, connections) in _connections)
            {
                foreach (var connection in connections)
                {
                    connection.Dispose();
                }
            }
            _connections.Clear();
        }

        private void StartHeartbeatTask(Guid userId, WebSocketConnection connection)
        {
            connection.HeartbeatCancellationTokenSource.Cancel();
            connection.HeartbeatCancellationTokenSource = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(heartbeatInterval, connection.HeartbeatCancellationTokenSource.Token);
                    await CloseConnectionAsync(userId, connection.WebSocket);
                }
                catch (TaskCanceledException)
                {
                    // Task was canceled, no action needed
                }
            });
        }

        public void RefreshConnection(Guid userId, WebSocketConnection connection)
        {
            StartHeartbeatTask(userId, connection);
        }

        public async Task RefreshConnectionAsync(WebSocket webSocket)
        {
            Guid userId = await GetUserIdAsync(webSocket);
            if (userId == Guid.Empty)
                return;

            WebSocketConnection? connection = GetConnection(userId, webSocket);
            if (connection == null)
                return;

            RefreshConnection(userId, connection);
        }

        public async Task<Guid> GetUserIdAsync(WebSocket webSocket)
        {
            WebSocketCache webSocketCache = await _cacheContext.
                Entry<WebSocketCache>(webSocket.GetHashCode().ToString()).
                GetOrSetAsync(
                    () => GetUserIdByWebSocket(webSocket),
                    TimeSpan.FromMinutes(10)
                );

            return webSocketCache.UserId;
        }

        public async Task<Guid> GetUserIdAsync(WebSocketConnection connection)
        {
            WebSocketCache webSocketCache = await _cacheContext.
                Entry<WebSocketCache>(connection.WebSocket.GetHashCode().ToString()).
                GetOrSetAsync(
                    () => GetUserIdByConnection(connection),
                    TimeSpan.FromMinutes(10)
                );

            return webSocketCache.UserId;
        }

        public WebSocketCache GetUserIdByConnection(WebSocketConnection connection)
        {

            WebSocketCache result = new WebSocketCache()
            {
                UserId = Guid.Empty
            };

            foreach (var kvp in _connections)
            {
                var userId = kvp.Key;
                var connections = kvp.Value;

                if (connections.Contains(connection))
                {
                    result.UserId = userId;
                    break;
                }
            }

            return result;
        }

        private WebSocketCache GetUserIdByWebSocket(WebSocket webSocket)
        {
            WebSocketCache result = new WebSocketCache()
            {
                UserId = Guid.Empty
            };

            foreach (var kvp in _connections)
            {
                var userId = kvp.Key;
                var connections = kvp.Value;

                var connection = connections.FirstOrDefault(conn => conn.WebSocket == webSocket);
                if (connection != null)
                {
                    result.UserId = userId;
                    break;
                }
            }

            return result;
        }
    }
}
