using System.Collections;
using System.Net.WebSockets;
using EchoPhase.Attributes;
using EchoPhase.Services.Bitmasks;

namespace EchoPhase.Services.WebSockets.Models
{
    public class WebSocketConnection : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();

        public WebSocket WebSocket { get; set; } = default!;
        public HttpContext HttpContext { get; set; } = default!;

        [AlwaysMerge]
        public BitArray Intents { get; set; } = BitMaskServiceBase.Empty;

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
