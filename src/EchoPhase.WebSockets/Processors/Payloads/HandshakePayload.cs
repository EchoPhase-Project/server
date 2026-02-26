using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;
using EchoPhase.Types.Validation;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.Handshake)]
    public class HandshakePayload : IPayload
    {
        public string? Intents { get; set; }

        public HandshakePayload()
        {
        }

        public IValidationResult Validate()
        {
            if (Intents != null && string.IsNullOrWhiteSpace(Intents))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), "Intents cannot be empty if set."));

            return ValidationResult.Success();
        }
    }
}
