using EchoPhase.Attributes;
using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Payloads
{
    [OpCodePayload(OpCodes.HandshakeAck)]
    public class HandshakeAckPayload : IPayload
    {
        public double HeartbeatInterval
        {
            get; set;
        }
        public string[] Intents { get; set; } = Array.Empty<string>();

        public HandshakeAckPayload()
        {
        }

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (HeartbeatInterval < 0)
            {
                errorMessage = "HeartbeatInterval cannot be zero or negative.";
                return false;
            }

            if (Intents is { Length: 0 })
            {
                errorMessage = "Intents cannot be empty.";
                return false;
            }

            return true;
        }
    }
}
