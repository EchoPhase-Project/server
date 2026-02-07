using EchoPhase.Attributes;
using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Payloads
{
    [OpCodePayload(OpCodes.Ping)]
    public class PingPayload : IPayload
    {
        public PingPayload()
        {
        }

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            return true;
        }
    }
}
