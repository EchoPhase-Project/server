using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class OpCodeHandlerAttribute : Attribute
    {
        public OpCodes OpCode
        {
            get;
        }

        public OpCodeHandlerAttribute(OpCodes opCode)
        {
            OpCode = opCode;
        }
    }
}
