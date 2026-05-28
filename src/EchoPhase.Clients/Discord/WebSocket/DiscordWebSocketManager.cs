// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EchoPhase.Clients.Providers;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Clients.Discord.WebSocket
{
    /// <summary>
    /// Manages Discord Gateway WebSocket connections per user/token and fans out
    /// incoming payloads to registered <see cref="IDiscordWebSocketSubscriber"/>s.
    /// </summary>
    /// <remarks>
    /// Internal structure:
    /// <code>
    /// _connections[userId] = {
    ///     tokenName -> [DiscordWebSocketConnection, ...]
    /// }
    /// _subscribers[userId] = [IDiscordWebSocketSubscriber, ...]
    /// </code>
    /// </remarks>
    public sealed class DiscordWebSocketManager : IDisposable
    {
        // ── Gateway constants ─────────────────────────────────────────────────

        private const string GatewayUrl = "wss://gateway.discord.gg/?v=10&encoding=json";
        private const int BufferSize = 4096;

        // ── State ─────────────────────────────────────────────────────────────

        // userId -> (tokenName -> connections[])
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, List<DiscordWebSocketConnection>>> _connections = new();

        // userId -> subscribers[]
        private readonly ConcurrentDictionary<Guid, List<IDiscordWebSocketSubscriber>> _subscribers = new();

        private readonly SemaphoreSlim _connectionsLock = new(1, 1);
        private readonly SemaphoreSlim _subscribersLock = new(1, 1);

        private readonly IClientTokenProvider _tokenProvider;
        private readonly ILogger<DiscordWebSocketManager> _logger;

        private bool _disposed;

        public DiscordWebSocketManager(
            IClientTokenProvider tokenProvider,
            ILogger<DiscordWebSocketManager> logger)
        {
            _tokenProvider = tokenProvider;
            _logger = logger;
        }

        // ── Connect ───────────────────────────────────────────────────────────

        /// <summary>
        /// Opens a new Discord Gateway WebSocket for <paramref name="userId"/> using the
        /// token identified by <paramref name="tokenName"/>.  Multiple connections for the
        /// same user/token pair are allowed and stored independently.
        /// </summary>
        /// <returns>The ID of the newly created connection.</returns>
        public async Task<Guid> ConnectAsync(
            Guid userId,
            string tokenName,
            CancellationToken ct = default)
        {
            var tokenBytes = await _tokenProvider.ResolveAsync(userId, tokenName);
            var token = Encoding.UTF8.GetString(tokenBytes);

            var connection = new DiscordWebSocketConnection(tokenName);

            await connection.WebSocket.ConnectAsync(new Uri(GatewayUrl), ct);

            _logger.LogInformation(
                "Discord Gateway connected. UserId={UserId} TokenName={TokenName} ConnectionId={ConnectionId}",
                userId, tokenName, connection.Id);

            await _connectionsLock.WaitAsync(ct);
            try
            {
                var userMap = _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, List<DiscordWebSocketConnection>>());

                userMap.AddOrUpdate(
                    tokenName,
                    _ => new List<DiscordWebSocketConnection> { connection },
                    (_, list) => { lock (list) { list.Add(connection); } return list; });
            }
            finally
            {
                _connectionsLock.Release();
            }

            // Fire-and-forget receive loop
            _ = Task.Run(() => ReceiveLoopAsync(userId, tokenName, connection, token), connection.CancellationTokenSource.Token);

            return connection.Id;
        }

        // ── Disconnect ────────────────────────────────────────────────────────

        /// <summary>
        /// Closes and removes a specific connection identified by <paramref name="connectionId"/>.
        /// </summary>
        public async Task DisconnectAsync(Guid userId, Guid connectionId)
        {
            await _connectionsLock.WaitAsync();
            try
            {
                if (!_connections.TryGetValue(userId, out var userMap))
                    return;

                foreach (var (tokenName, list) in userMap)
                {
                    DiscordWebSocketConnection? found = null;
                    lock (list)
                    {
                        found = list.FirstOrDefault(c => c.Id == connectionId);
                        if (found is not null)
                            list.Remove(found);

                        if (list.Count == 0)
                            userMap.TryRemove(tokenName, out _);
                    }

                    if (found is not null)
                    {
                        found.Dispose();
                        _logger.LogInformation(
                            "Discord Gateway disconnected. UserId={UserId} ConnectionId={ConnectionId}",
                            userId, connectionId);
                        return;
                    }
                }
            }
            finally
            {
                _connectionsLock.Release();
            }
        }

        /// <summary>
        /// Closes all connections for a given user and token name.
        /// </summary>
        public async Task DisconnectAllAsync(Guid userId, string tokenName)
        {
            await _connectionsLock.WaitAsync();
            try
            {
                if (!_connections.TryGetValue(userId, out var userMap))
                    return;

                if (!userMap.TryRemove(tokenName, out var list))
                    return;

                lock (list)
                {
                    foreach (var conn in list)
                        conn.Dispose();
                }
            }
            finally
            {
                _connectionsLock.Release();
            }
        }

        /// <summary>
        /// Closes all connections for a given user across all token names.
        /// </summary>
        public async Task DisconnectUserAsync(Guid userId)
        {
            await _connectionsLock.WaitAsync();
            try
            {
                if (!_connections.TryRemove(userId, out var userMap))
                    return;

                foreach (var (_, list) in userMap)
                    lock (list) { foreach (var c in list) c.Dispose(); }
            }
            finally
            {
                _connectionsLock.Release();
            }
        }

        // ── Subscribe / Unsubscribe ───────────────────────────────────────────

        /// <summary>
        /// Registers <paramref name="subscriber"/> to receive payloads for
        /// <paramref name="userId"/>.
        /// Returns a <see cref="DiscordWebSocketSubscription"/> — dispose it to unsubscribe.
        /// </summary>
        public DiscordWebSocketSubscription Subscribe(Guid userId, IDiscordWebSocketSubscriber subscriber)
        {
            _subscribersLock.Wait();
            try
            {
                var list = _subscribers.GetOrAdd(userId, _ => new List<IDiscordWebSocketSubscriber>());
                lock (list)
                {
                    if (!list.Contains(subscriber))
                        list.Add(subscriber);
                }
            }
            finally
            {
                _subscribersLock.Release();
            }

            _logger.LogDebug("Subscriber registered for UserId={UserId}", userId);
            return new DiscordWebSocketSubscription(this, userId, subscriber);
        }

        /// <summary>
        /// Removes <paramref name="subscriber"/> from the notification list for
        /// <paramref name="userId"/>.  Called automatically when a
        /// <see cref="DiscordWebSocketSubscription"/> is disposed.
        /// </summary>
        public void Unsubscribe(Guid userId, IDiscordWebSocketSubscriber subscriber)
        {
            _subscribersLock.Wait();
            try
            {
                if (!_subscribers.TryGetValue(userId, out var list))
                    return;

                lock (list)
                {
                    list.Remove(subscriber);
                    if (list.Count == 0)
                        _subscribers.TryRemove(userId, out _);
                }
            }
            finally
            {
                _subscribersLock.Release();
            }

            _logger.LogDebug("Subscriber unregistered for UserId={UserId}", userId);
        }

        // ── Query ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns all active connections for a user, keyed by token name.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<Guid>> GetConnections(Guid userId)
        {
            if (!_connections.TryGetValue(userId, out var userMap))
                return new Dictionary<string, IReadOnlyList<Guid>>();

            var result = new Dictionary<string, IReadOnlyList<Guid>>();
            foreach (var (tokenName, list) in userMap)
                lock (list) { result[tokenName] = list.Select(c => c.Id).ToList(); }

            return result;
        }

        // ── Receive loop ──────────────────────────────────────────────────────

        private async Task ReceiveLoopAsync(
            Guid userId,
            string tokenName,
            DiscordWebSocketConnection connection,
            string token)
        {
            var buffer = new byte[BufferSize];
            var sb = new StringBuilder();
            var ct = connection.CancellationTokenSource.Token;

            try
            {
                // Discord sends a Hello payload immediately on connect;
                // then we need to Identify before we get real events.
                await IdentifyAsync(connection, token, ct);

                while (!ct.IsCancellationRequested &&
                       connection.WebSocket.State == WebSocketState.Open)
                {
                    sb.Clear();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await connection.WebSocket.ReceiveAsync(
                            new ArraySegment<byte>(buffer), ct);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _logger.LogInformation(
                                "Discord Gateway close received. UserId={UserId} ConnectionId={ConnectionId}",
                                userId, connection.Id);
                            return;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    var rawJson = sb.ToString();

                    JsonDocument? doc = null;
                    try { doc = JsonDocument.Parse(rawJson); }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse Discord payload JSON");
                        continue;
                    }

                    await HandleSystemPayloadAsync(connection, doc, token, ct);
                    await TriggerSubscribersAsync(userId, tokenName, connection.Id, doc);
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex,
                    "Discord Gateway WebSocket error. UserId={UserId} ConnectionId={ConnectionId}",
                    userId, connection.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error in Discord Gateway receive loop. UserId={UserId} ConnectionId={ConnectionId}",
                    userId, connection.Id);
            }
            finally
            {
                // Clean up the connection entry so it doesn't linger
                await DisconnectAsync(userId, connection.Id);
            }
        }

        // ── Discord protocol helpers ──────────────────────────────────────────

        private static async Task IdentifyAsync(
            DiscordWebSocketConnection connection,
            string token,
            CancellationToken ct)
        {
            // Wait for the Hello (op 10) before sending Identify (op 2)
            var buffer = new byte[BufferSize];
            var result = await connection.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            var hello = Encoding.UTF8.GetString(buffer, 0, result.Count);

            using var helloDoc = JsonDocument.Parse(hello);
            if (!helloDoc.RootElement.TryGetProperty("op", out var opProp) || opProp.GetInt32() != 10)
                throw new InvalidOperationException("Expected Discord Hello (op 10), got: " + hello);

            var identify = JsonSerializer.Serialize(new
            {
                op = 2,
                d = new
                {
                    token,
                    intents = 0,          // caller should adjust via payload / config
                    properties = new
                    {
                        os = "linux",
                        browser = "echophase",
                        device = "echophase"
                    }
                }
            });

            var bytes = Encoding.UTF8.GetBytes(identify);
            await connection.WebSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                ct);
        }

        private static async Task HandleSystemPayloadAsync(
            DiscordWebSocketConnection connection,
            JsonDocument doc,
            string token,
            CancellationToken ct)
        {
            if (!doc.RootElement.TryGetProperty("op", out var opProp))
                return;

            var op = opProp.GetInt32();

            // Heartbeat request (op 1) → respond with Heartbeat ACK body
            if (op == 1)
            {
                long? seq = null;
                if (doc.RootElement.TryGetProperty("d", out var d) && d.ValueKind == JsonValueKind.Number)
                    seq = d.GetInt64();

                var heartbeat = JsonSerializer.Serialize(new { op = 1, d = seq });
                var bytes = Encoding.UTF8.GetBytes(heartbeat);
                await connection.WebSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    ct);
            }
        }

        // ── Fan-out ───────────────────────────────────────────────────────────

        private async Task TriggerSubscribersAsync(
            Guid userId,
            string tokenName,
            Guid connectionId,
            JsonDocument payload)
        {
            if (!_subscribers.TryGetValue(userId, out var list))
                return;

            IDiscordWebSocketSubscriber[] snapshot;
            lock (list) { snapshot = list.ToArray(); }

            foreach (var subscriber in snapshot)
            {
                try
                {
                    await subscriber.OnPayloadAsync(userId, tokenName, connectionId, payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Subscriber threw while handling Discord payload. UserId={UserId}", userId);
                }
            }
        }

        // ── IDisposable ───────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var (_, userMap) in _connections)
                foreach (var (_, list) in userMap)
                    lock (list) { foreach (var c in list) c.Dispose(); }

            _connections.Clear();
            _subscribers.Clear();
            _connectionsLock.Dispose();
            _subscribersLock.Dispose();
        }
    }
}
