using EchoPhase.Types.Validation;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    public interface IPayload
    {
        IValidationResult Validate();
    }
}
