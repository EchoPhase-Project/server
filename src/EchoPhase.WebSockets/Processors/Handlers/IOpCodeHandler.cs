using System.Net.WebSockets;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    public interface IOpCodeHandler
    {
        Task HandleAsync(WebSocket socket, object payload);
    }

    public interface IOpCodeHandler<TPayload> : IOpCodeHandler
    {
        Task HandleAsync(WebSocket socket, TPayload payload);
    }
}
