using EchoPhase.Attributes;
using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Payloads
{
    [OpCodePayload(OpCodes.Handshake)]
    public class HandshakePayload : IPayload
    {
        public string? Intents
        {
            get; set;
        }

        public HandshakePayload()
        {
        }

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (Intents != null && string.IsNullOrWhiteSpace(Intents))
            {
                errorMessage = "Intents cannot be empty if set";
                return false;
            }

            return true;
        }
    }
}
