using EchoPhase.Types.Validation;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.DisconnectAck)]
    public class DisconnectAckPayload : IPayload
    {
        public DisconnectAckPayload()
        {
        }

        public IValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
