using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    public interface IEventMessage
    {
        OpCodes Op
        {
            get;
        }
    }

    public interface IEventMessage<T> : IEventMessage
    {
        T D
        {
            get;
        }
    }
}
