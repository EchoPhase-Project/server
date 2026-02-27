using EchoPhase.Types.Validation;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.Disconnect)]
    public class DisconnectPayload : IPayload
    {
        public DisconnectPayload()
        {
        }

        public IValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
