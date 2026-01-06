using EchoPhase.Processors.Enums;

namespace EchoPhase.Attributes
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
