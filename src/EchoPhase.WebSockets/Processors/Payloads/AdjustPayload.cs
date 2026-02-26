using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;
using EchoPhase.Types.Validation;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.Adjust)]
    public class AdjustPayload : IPayload
    {
        public string Intents { get; set; } = string.Empty;

        public AdjustPayload()
        {
        }

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Intents))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), "Intents cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
