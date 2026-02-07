using EchoPhase.Attributes;
using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Payloads
{
    [OpCodePayload(OpCodes.Error)]
    public class ErrorPayload : IPayload
    {
        public ErrorCodes Code { get; set; } = ErrorCodes.Undefined;
        public string Message { get; set; } = string.Empty;

        public ErrorPayload()
        {
        }

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Message))
            {
                errorMessage = "Message cannot be null or empty.";
                return false;
            }

            return true;
        }
    }
}
