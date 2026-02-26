using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;
using EchoPhase.Types.Validation;

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
