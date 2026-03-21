// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;

namespace EchoPhase.WebSockets.Exceptions
{
    public class WebSocketConnectionNotFoundException : Exception
    {
        public WebSocketConnectionNotFoundException(Guid id)
            : base($"WebSocketConnections for UserID {id} was not found.")
        {
        }

        public WebSocketConnectionNotFoundException(Guid id, WebSocket webSocket)
            : base($"WebSocketConnection for UserID {id} and WebSocket {webSocket.GetHashCode().ToString()} was not found.")
        {
        }
    }
}
