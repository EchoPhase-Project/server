using EchoPhase.Types.Validation;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors.Payloads
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

        public IValidationResult Validate()
        {
            if (HeartbeatInterval <= 0)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(HeartbeatInterval), "HeartbeatInterval must be positive."));

            if (Intents is { Length: 0 })
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), "Intents cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
