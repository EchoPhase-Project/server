// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Clients.Discord.WebSocket
{
    /// <summary>
    /// Discord gateway connections per user and token name, fanning dispatch
    /// payloads out to <see cref="IDiscordWebSocketSubscriber"/>s.
    /// </summary>
    /// <remarks>
    /// Each connection runs a session loop: Hello, heartbeats, Identify (or
    /// Resume), then dispatches until the socket closes. What follows depends
    /// on why (<see cref="DiscordGatewayCloseCodes"/>): resume the session
    /// (Discord replays missed events), start a new one, or stop for good and
    /// tell the <see cref="IDiscordGatewayObserver"/>s. Reconnects back off
    /// from 1 s to 60 s; a session that got going resets the backoff.
    /// </remarks>
    public sealed class DiscordWebSocketManager : IDisposable
    {
        private const string GatewayUrl = "wss://gateway.discord.gg/?v=10&encoding=json";
        private const string GatewayQuery = "?v=10&encoding=json";
        private const int BufferSize = 4096;
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

        /// <summary>Why one socket ended, and what to do next.</summary>
        private readonly record struct SocketOutcome(DiscordGatewayRecovery Recovery, int? CloseCode, string Message, bool Started);

        // userId -> (tokenName -> connections)
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, List<DiscordWebSocketConnection>>> _connections = new();
        private readonly ConcurrentDictionary<Guid, List<IDiscordWebSocketSubscriber>> _subscribers = new();
        private readonly List<IDiscordGatewayObserver> _observers = [];
        private readonly ILogger<DiscordWebSocketManager> _logger;
        private bool _disposed;

        public DiscordWebSocketManager(ILogger<DiscordWebSocketManager> logger)
        {
            _logger = logger;
        }

        // ── Connect / disconnect ──────────────────────────────────────────────

        /// <summary>
        /// Opens a gateway connection for <paramref name="userId"/> with the bot
        /// <paramref name="token"/> of the connection named <paramref name="tokenName"/>.
        /// The first socket is opened here (so an unreachable gateway fails the
        /// call); the session then runs, and reconnects, in the background.
        /// </summary>
        /// <param name="intents">Which events to receive (<see cref="DiscordGatewayIntents"/>).</param>
        /// <returns>The connection's id; it stays the same across reconnects.</returns>
        public async Task<Guid> ConnectAsync(
            Guid userId,
            string tokenName,
            string token,
            int intents = DiscordGatewayIntents.Default,
            CancellationToken ct = default)
        {
            var connection = new DiscordWebSocketConnection(tokenName, intents);
            await connection.WebSocket.ConnectAsync(new Uri(GatewayUrl), ct);

            var userMap = _connections.GetOrAdd(userId, _ => new());
            var list = userMap.GetOrAdd(tokenName, _ => []);
            lock (list)
                list.Add(connection);

            _logger.LogInformation("Discord Gateway connected. UserId={UserId} TokenName={TokenName} ConnectionId={ConnectionId}",
                userId, tokenName, connection.Id);

            _ = Task.Run(() => SessionLoopAsync(userId, connection, token));
            return connection.Id;
        }

        /// <summary>Closes and forgets one connection (ends its session at Discord).</summary>
        public async Task DisconnectAsync(Guid userId, Guid connectionId)
        {
            if (Find(userId, connectionId) is { } connection)
                await StopAsync(userId, connection, normal: true);
        }

        /// <summary>Closes all connections of a user for one token name.</summary>
        public Task DisconnectAllAsync(Guid userId, string tokenName)
            => Task.WhenAll(Snapshot(userId).Where(c => c.TokenName == tokenName).Select(c => StopAsync(userId, c, normal: true)));

        /// <summary>Closes all connections of a user.</summary>
        public Task DisconnectUserAsync(Guid userId)
            => Task.WhenAll(Snapshot(userId).Select(c => StopAsync(userId, c, normal: true)));

        /// <summary>Open connections of a user by token name.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<Guid>> GetConnections(Guid userId)
        {
            var result = new Dictionary<string, IReadOnlyList<Guid>>();
            if (_connections.TryGetValue(userId, out var userMap))
                foreach (var (tokenName, list) in userMap)
                    lock (list)
                        if (list.Count > 0)
                            result[tokenName] = list.Select(c => c.Id).ToList();
            return result;
        }

        // ── Subscribers and observers ─────────────────────────────────────────

        /// <summary>Receives the user's dispatches; dispose the result to unsubscribe.</summary>
        public DiscordWebSocketSubscription Subscribe(Guid userId, IDiscordWebSocketSubscriber subscriber)
        {
            var list = _subscribers.GetOrAdd(userId, _ => []);
            lock (list)
                if (!list.Contains(subscriber))
                    list.Add(subscriber);
            return new DiscordWebSocketSubscription(this, userId, subscriber);
        }

        public void Unsubscribe(Guid userId, IDiscordWebSocketSubscriber subscriber)
        {
            if (_subscribers.TryGetValue(userId, out var list))
                lock (list)
                    list.Remove(subscriber);
        }

        /// <summary>Tells <paramref name="observer"/> about connections that stop on a fatal error.</summary>
        public void AddObserver(IDiscordGatewayObserver observer)
        {
            lock (_observers)
                _observers.Add(observer);
        }

        // ── Session ───────────────────────────────────────────────────────────

        private async Task SessionLoopAsync(Guid userId, DiscordWebSocketConnection connection, string token)
        {
            var ct = connection.CancellationTokenSource.Token;
            int failures = 0;
            bool fresh = true; // the first socket is already connected

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    SocketOutcome outcome;
                    try
                    {
                        if (!fresh)
                        {
                            var url = connection.SessionId is not null && connection.ResumeUrl is not null
                                ? connection.ResumeUrl
                                : GatewayUrl;
                            await connection.NewSocket().ConnectAsync(new Uri(url), ct);
                        }
                        fresh = false;
                        outcome = await RunSocketAsync(userId, connection, token, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex) when (ex is WebSocketException or JsonException or InvalidOperationException or KeyNotFoundException)
                    {
                        outcome = new SocketOutcome(DiscordGatewayRecovery.Resume, null, ex.Message, Started: false);
                    }

                    if (connection.Stopping)
                        return;

                    if (outcome.Recovery == DiscordGatewayRecovery.Fatal)
                    {
                        _logger.LogError("Discord Gateway stopped: {Message} (close {Code}). UserId={UserId} ConnectionId={ConnectionId}",
                            outcome.Message, outcome.CloseCode, userId, connection.Id);
                        await NotifyFailedAsync(new DiscordGatewayFailure(userId, connection.TokenName, connection.Id, outcome.CloseCode, outcome.Message));
                        await StopAsync(userId, connection, normal: false);
                        return;
                    }

                    if (outcome.Recovery == DiscordGatewayRecovery.Reidentify)
                        connection.ForgetSession();

                    failures = outcome.Started ? 1 : failures + 1;
                    var delay = Backoff(failures);
                    _logger.LogWarning("Discord Gateway {Action} in {Delay:F1}s: {Message} (close {Code}). ConnectionId={ConnectionId}",
                        connection.SessionId is null ? "reconnecting" : "resuming", delay.TotalSeconds, outcome.Message,
                        outcome.CloseCode, connection.Id);
                    await Task.Delay(delay, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Disconnected.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord Gateway session failed. UserId={UserId} ConnectionId={ConnectionId}", userId, connection.Id);
                await StopAsync(userId, connection, normal: false);
            }
        }

        // 1 s, 2 s, 4 s ... 60 s, with up to 50% random jitter so reconnects spread out.
        private static TimeSpan Backoff(int failures)
        {
            var seconds = Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, Math.Max(0, failures - 1)));
            return TimeSpan.FromSeconds(seconds * (1 + Random.Shared.NextDouble() / 2));
        }

        // One socket's life: Hello, heartbeats, Identify or Resume, dispatches.
        private async Task<SocketOutcome> RunSocketAsync(
            Guid userId, DiscordWebSocketConnection connection, string token, CancellationToken ct)
        {
            using var socketCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bool started = false;

            using (var hello = await ReceiveDocumentAsync(connection, ct))
            {
                if (hello is null)
                    return Closed(connection, started);
                if (!hello.RootElement.TryGetProperty("op", out var op) || op.GetInt32() != 10)
                    return new SocketOutcome(DiscordGatewayRecovery.Reidentify, null, "Expected Hello (op 10)", started);

                var interval = TimeSpan.FromMilliseconds(hello.RootElement.GetProperty("d").GetProperty("heartbeat_interval").GetDouble());
                _ = Task.Run(() => HeartbeatLoopAsync(connection, interval, socketCts.Token));
            }

            if (connection.SessionId is { } sessionId)
                await connection.SendAsync(JsonSerializer.Serialize(new
                {
                    op = 6,
                    d = new { token, session_id = sessionId, seq = connection.Sequence }
                }), ct);
            else
                await connection.SendAsync(JsonSerializer.Serialize(new
                {
                    op = 2,
                    d = new
                    {
                        token,
                        intents = connection.Intents,
                        properties = new { os = "linux", browser = "echophase", device = "echophase" }
                    }
                }), ct);

            try
            {
                while (true)
                {
                    using var doc = await ReceiveDocumentAsync(connection, ct);
                    if (doc is null)
                        return Closed(connection, started);

                    var root = doc.RootElement;
                    if (root.TryGetProperty("s", out var s) && s.ValueKind == JsonValueKind.Number)
                        connection.Sequence = s.GetInt64();

                    switch (root.GetProperty("op").GetInt32())
                    {
                        case 0:
                            var type = root.TryGetProperty("t", out var t) ? t.GetString() : null;
                            if (type == "READY")
                            {
                                var d = root.GetProperty("d");
                                connection.SessionId = d.GetProperty("session_id").GetString();
                                connection.ResumeUrl = d.TryGetProperty("resume_gateway_url", out var url)
                                    ? url.GetString()?.TrimEnd('/') + "/" + GatewayQuery
                                    : null;
                                started = true;
                            }
                            else if (type == "RESUMED")
                            {
                                started = true;
                                _logger.LogInformation("Discord Gateway resumed. ConnectionId={ConnectionId}", connection.Id);
                            }
                            await DispatchAsync(userId, connection, doc, ct);
                            break;
                        case 1:
                            await SendHeartbeatAsync(connection, ct);
                            break;
                        case 7:
                            // Reconnect: close keeping the session (non-1000), then resume.
                            await connection.CloseSocketAsync(4000, "Reconnect requested");
                            return new SocketOutcome(DiscordGatewayRecovery.Resume, null, "Discord asked to reconnect", started);
                        case 9:
                            // Invalid session: d says whether it can still be resumed.
                            bool resumable = root.TryGetProperty("d", out var r) && r.ValueKind == JsonValueKind.True;
                            await connection.CloseSocketAsync(4000, "Invalid session");
                            return new SocketOutcome(
                                resumable ? DiscordGatewayRecovery.Resume : DiscordGatewayRecovery.Reidentify,
                                null, "Invalid session", started);
                        case 11:
                            connection.Acknowledge();
                            break;
                    }
                }
            }
            finally
            {
                socketCts.Cancel();
            }
        }

        private static SocketOutcome Closed(DiscordWebSocketConnection connection, bool started)
        {
            var code = (int?)connection.WebSocket.CloseStatus;
            var (recovery, message) = DiscordGatewayCloseCodes.Classify(code);
            return new SocketOutcome(recovery, code, message, started);
        }

        // Heartbeats every interval (the first after a random part of it, as the
        // protocol asks). An unacknowledged beat means a zombie socket: close it
        // so the session loop resumes on a new one.
        private async Task HeartbeatLoopAsync(DiscordWebSocketConnection connection, TimeSpan interval, CancellationToken ct)
        {
            try
            {
                await Task.Delay(interval * Random.Shared.NextDouble(), ct);
                while (!ct.IsCancellationRequested)
                {
                    if (!connection.TakeAck())
                    {
                        _logger.LogWarning("Discord heartbeat not acknowledged; reconnecting. ConnectionId={ConnectionId}", connection.Id);
                        await connection.CloseSocketAsync(4000, "Heartbeat not acknowledged");
                        return;
                    }
                    await SendHeartbeatAsync(connection, ct);
                    await Task.Delay(interval, ct);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
            {
                // The socket ended; the session loop handles it.
            }
        }

        private static Task SendHeartbeatAsync(DiscordWebSocketConnection connection, CancellationToken ct)
            => connection.SendAsync(JsonSerializer.Serialize(new { op = 1, d = connection.Sequence }), ct);

        private static async Task<JsonDocument?> ReceiveDocumentAsync(DiscordWebSocketConnection connection, CancellationToken ct)
        {
            var buffer = new byte[BufferSize];
            using var message = new MemoryStream();
            ValueWebSocketReceiveResult result;
            do
            {
                result = await connection.WebSocket.ReceiveAsync(buffer.AsMemory(), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;
                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
        }

        // ── Fan-out ───────────────────────────────────────────────────────────

        private async Task DispatchAsync(Guid userId, DiscordWebSocketConnection connection, JsonDocument payload, CancellationToken ct)
        {
            if (!_subscribers.TryGetValue(userId, out var list))
                return;

            IDiscordWebSocketSubscriber[] snapshot;
            lock (list)
                snapshot = [.. list];

            foreach (var subscriber in snapshot)
            {
                try
                {
                    await subscriber.OnPayloadAsync(userId, connection.TokenName, connection.Id, payload, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Subscriber threw while handling Discord payload. UserId={UserId}", userId);
                }
            }
        }

        private async Task NotifyFailedAsync(DiscordGatewayFailure failure)
        {
            IDiscordGatewayObserver[] observers;
            lock (_observers)
                observers = [.. _observers];

            foreach (var observer in observers)
            {
                try
                {
                    await observer.OnFailedAsync(failure);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Discord gateway observer {Observer} failed.", observer.GetType().Name);
                }
            }
        }

        // ── Housekeeping ──────────────────────────────────────────────────────

        private async Task StopAsync(Guid userId, DiscordWebSocketConnection connection, bool normal)
        {
            connection.Stopping = true;
            if (_connections.TryGetValue(userId, out var userMap) && userMap.TryGetValue(connection.TokenName, out var list))
                lock (list)
                    list.Remove(connection);

            if (normal)
                await connection.CloseSocketAsync(1000, "Disconnected");
            connection.Dispose();

            _logger.LogInformation("Discord Gateway disconnected. UserId={UserId} ConnectionId={ConnectionId}", userId, connection.Id);
        }

        private DiscordWebSocketConnection? Find(Guid userId, Guid connectionId)
            => Snapshot(userId).FirstOrDefault(c => c.Id == connectionId);

        private List<DiscordWebSocketConnection> Snapshot(Guid userId)
        {
            var result = new List<DiscordWebSocketConnection>();
            if (_connections.TryGetValue(userId, out var userMap))
                foreach (var list in userMap.Values)
                    lock (list)
                        result.AddRange(list);
            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var userMap in _connections.Values)
                foreach (var list in userMap.Values)
                    lock (list)
                    {
                        foreach (var connection in list)
                            connection.Dispose();
                        list.Clear();
                    }

            _connections.Clear();
            _subscribers.Clear();
        }
    }
}
