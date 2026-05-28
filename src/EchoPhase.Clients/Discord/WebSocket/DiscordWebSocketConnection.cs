// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;

namespace EchoPhase.Clients.Discord.WebSocket
{
    /// <summary>
    /// Represents a single WebSocket connection to the Discord Gateway
    /// for a specific token.
    /// </summary>
    public sealed class DiscordWebSocketConnection : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>The token name used to establish this connection.</summary>
        public string TokenName { get; }

        public ClientWebSocket WebSocket { get; } = new();

        public CancellationTokenSource CancellationTokenSource { get; } = new();

        private bool _disposed;

        public DiscordWebSocketConnection(string tokenName)
        {
            TokenName = tokenName;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CancellationTokenSource.Cancel();
            CancellationTokenSource.Dispose();

            if (WebSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    WebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Disposing",
                        CancellationToken.None).GetAwaiter().GetResult();
                }
                catch { /* best-effort */ }
            }

            WebSocket.Dispose();
        }
    }
}
