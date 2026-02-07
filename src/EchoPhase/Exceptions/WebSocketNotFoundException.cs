using System.Net.WebSockets;

namespace EchoPhase.Exceptions
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
