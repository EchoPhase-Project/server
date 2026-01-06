using EchoPhase.Attributes;
using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Payloads
{
    [OpCodePayload(OpCodes.Disconnect)]
    public class DisconnectPayload : IPayload
    {
        public DisconnectPayload()
        {
        }

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            return true;
        }
    }
}
