using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;
using EchoPhase.Types.Validation;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.AdjustAck)]
    public class AdjustAckPayload : IPayload
    {
        public AdjustAckPayload()
        {
        }

        public IValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
