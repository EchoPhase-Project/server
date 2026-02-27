using EchoPhase.Types.Validation;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;

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
