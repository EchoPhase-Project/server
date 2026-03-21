// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Net.WebSockets;
using EchoPhase.Types.Attributes;
using EchoPhase.Types.BitMask;
using Microsoft.AspNetCore.Http;
using UUIDNext;

namespace EchoPhase.WebSockets
{
    public class WebSocketConnection : IDisposable
    {
        public Guid Id { get; } = Uuid.NewSequential();

        public WebSocket WebSocket { get; set; } = default!;
        public HttpContext HttpContext { get; set; } = default!;

        [AlwaysMerge]
        public BitArray Intents { get; set; } = BitMaskBase.Empty;

        public CancellationTokenSource HeartbeatCancellationTokenSource { get; set; } = new();

        private bool _disposed = false;

        /// <summary>
        /// Releases all resources used by the WebSocketConnection.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="disposing">True to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Dispose managed resources
                HeartbeatCancellationTokenSource?.Cancel();
                HeartbeatCancellationTokenSource?.Dispose();

                if (WebSocket is not null && WebSocket.State != WebSocketState.Closed && WebSocket.State != WebSocketState.Aborted)
                {
                    try { WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposed", CancellationToken.None).GetAwaiter().GetResult(); } catch { }
                }

                WebSocket?.Dispose();
            }

            _disposed = true;
        }
    }
}
