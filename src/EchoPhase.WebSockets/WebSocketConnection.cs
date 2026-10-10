// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Net.WebSockets;
using System.Security.Claims;
using EchoPhase.Types.Attributes;
using EchoPhase.Types.BitMask;
using UUIDNext;

namespace EchoPhase.WebSockets
{
    /// <summary>
    /// One client socket. Sends go through <see cref="SendAsync"/>: replies,
    /// events and broadcasts can come from different threads at once, and a
    /// WebSocket allows one outstanding send.
    /// </summary>
    public sealed class WebSocketConnection : IDisposable
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private long _lastSeen;
        private int _closing;

        public WebSocketConnection(Guid userId, WebSocket webSocket, ClaimsPrincipal user, long now)
        {
            UserId = userId;
            WebSocket = webSocket;
            User = user;
            _lastSeen = now;
        }

        public Guid Id { get; } = Uuid.NewSequential();

        public Guid UserId { get; }

        public WebSocket WebSocket { get; }

        /// <summary>The principal of the token the socket was opened with; re-checked by the credential monitor.</summary>
        public ClaimsPrincipal User { get; }

        /// <summary>Which events it receives; replaced as a whole on handshake/adjust.</summary>
        [AlwaysMerge]
        public BitArray Intents
        {
            get => Volatile.Read(ref field);
            set => Volatile.Write(ref field, value);
        } = BitMaskBase.Empty;

        /// <summary>Timestamp (TimeProvider) of the last message from the client.</summary>
        public long LastSeen => Interlocked.Read(ref _lastSeen);

        public bool IsOpen => WebSocket.State == WebSocketState.Open && Volatile.Read(ref _closing) == 0;

        public void Touch(long now) => Interlocked.Exchange(ref _lastSeen, now);

        public async Task SendAsync(ReadOnlyMemory<byte> utf8Json, CancellationToken ct)
        {
            await _sendLock.WaitAsync(ct);
            try
            {
                if (IsOpen)
                    await WebSocket.SendAsync(utf8Json, WebSocketMessageType.Text, endOfMessage: true, ct);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// Starts the closing handshake once (later calls do nothing). Uses
        /// CloseOutput: the receive loop owns reading and sees the client's reply.
        /// </summary>
        public async Task CloseAsync(WebSocketCloseStatus status, string description, TimeSpan timeout)
        {
            if (Interlocked.Exchange(ref _closing, 1) == 1)
                return;

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await _sendLock.WaitAsync(cts.Token);
                try
                {
                    if (WebSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                        await WebSocket.CloseOutputAsync(status, description, cts.Token);
                }
                finally
                {
                    _sendLock.Release();
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
            {
                WebSocket.Abort();
            }
        }

        public void Dispose() => _sendLock.Dispose();
    }
}
