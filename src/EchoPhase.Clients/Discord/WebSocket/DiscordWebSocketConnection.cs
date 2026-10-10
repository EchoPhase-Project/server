// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using System.Text;

namespace EchoPhase.Clients.Discord.WebSocket
{
    /// <summary>
    /// One logical gateway connection for a token: it keeps its id, session
    /// and sequence across reconnects, while the socket underneath is
    /// replaced each time.
    /// </summary>
    public sealed class DiscordWebSocketConnection : IDisposable
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private long _sequence = -1;
        private int _acked = 1;
        private bool _disposed;

        public DiscordWebSocketConnection(string tokenName, int intents)
        {
            TokenName = tokenName;
            Intents = intents;
        }

        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>The token name used to establish this connection.</summary>
        public string TokenName { get; }

        /// <summary>The intents it identifies with.</summary>
        public int Intents { get; }

        /// <summary>The current socket; replaced on reconnect.</summary>
        public ClientWebSocket WebSocket { get; private set; } = new();

        /// <summary>Cancelled when the connection is closed for good.</summary>
        public CancellationTokenSource CancellationTokenSource { get; } = new();

        /// <summary>Set once it is being closed for good: the session loop must not reconnect.</summary>
        public bool Stopping
        {
            get => Volatile.Read(ref _stopping) == 1;
            set => Volatile.Write(ref _stopping, value ? 1 : 0);
        }

        private int _stopping;

        /// <summary>From READY: what Resume needs.</summary>
        public string? SessionId { get; set; }

        public string? ResumeUrl { get; set; }

        /// <summary>The last dispatch sequence number ("s"), sent back in heartbeats and on resume.</summary>
        public long? Sequence
        {
            get { var s = Interlocked.Read(ref _sequence); return s < 0 ? null : s; }
            set => Interlocked.Exchange(ref _sequence, value ?? -1);
        }

        /// <summary>Whether the last heartbeat was acknowledged (op 11); a missing ACK means a dead socket.</summary>
        public bool TakeAck() => Interlocked.Exchange(ref _acked, 0) == 1;

        public void Acknowledge() => Interlocked.Exchange(ref _acked, 1);

        public void ForgetSession()
        {
            SessionId = null;
            ResumeUrl = null;
            Sequence = null;
        }

        /// <summary>A fresh socket for the next attempt; the old one is dropped.</summary>
        public ClientWebSocket NewSocket()
        {
            var old = WebSocket;
            WebSocket = new ClientWebSocket();
            Acknowledge();
            old.Abort();
            old.Dispose();
            return WebSocket;
        }

        /// <summary>
        /// Sends one text message. Serialized: the heartbeat loop and replies
        /// share the socket, and a WebSocket allows one send at a time.
        /// </summary>
        public async Task SendAsync(string json, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _sendLock.WaitAsync(ct);
            try
            {
                await WebSocket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// Closes the socket with <paramref name="code"/>: 1000 ends the
        /// session at Discord; any 4000-range code keeps it resumable.
        /// </summary>
        public async Task CloseSocketAsync(int code, string reason)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (WebSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await WebSocket.CloseOutputAsync((WebSocketCloseStatus)code, reason, timeout.Token);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
            {
                WebSocket.Abort();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();
            WebSocket.Abort();
            WebSocket.Dispose();
            _sendLock.Dispose();
        }
    }
}
