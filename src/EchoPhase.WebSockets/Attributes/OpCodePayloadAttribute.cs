using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class OpCodePayloadAttribute : Attribute
    {
        public OpCodes OpCode
        {
            get;
        }

        public OpCodePayloadAttribute(OpCodes opCode)
        {
            OpCode = opCode;
        }
    }
}
