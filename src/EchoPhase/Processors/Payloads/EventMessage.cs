using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Payloads
{
    public class EventMessage : IEventMessage
    {
        public OpCodes Op { get; private set; } = OpCodes.Unknown;

        public EventMessage()
        {
        }

        public EventMessage(OpCodes op)
        {
            Op = op;
        }

        public static EventMessage Create(OpCodes op) =>
            new(op);
    }

    public class EventMessage<T> : EventMessage, IEventMessage<T> where T : new()
    {
        public T D
        {
            get; private set;
        }

        public EventMessage(OpCodes op, T d) : base(op)
        {
            D = d;
        }

        public static EventMessage<T> Create(OpCodes op, T data) =>
            new(op, data);

        public static EventMessage<T> Create(OpCodes op, Action<T> configure)
        {
            var data = new T();
            configure(data);
            return new EventMessage<T>(op, data);
        }
    }

    public class RawEventMessage : IEventMessage
    {
        public OpCodes Op { get; set; } = OpCodes.Unknown;

        public object? D
        {
            get; set;
        }

        public RawEventMessage()
        {
        }

        public RawEventMessage(OpCodes op, object? d = null)
        {
            Op = op;
            D = d;
        }
    }
}
