using EchoPhase.Processors.Enums;

namespace EchoPhase.Interfaces
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
