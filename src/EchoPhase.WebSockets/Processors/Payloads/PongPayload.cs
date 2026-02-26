using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;
using EchoPhase.Types.Validation;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.Pong)]
    public class PongPayload : IPayload
    {
        public PongPayload()
        {
        }

        public IValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
