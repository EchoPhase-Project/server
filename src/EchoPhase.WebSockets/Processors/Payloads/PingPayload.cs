using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;
using EchoPhase.Types.Validation;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.Ping)]
    public class PingPayload : IPayload
    {
        public PingPayload()
        {
        }

        public IValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
