using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Payloads
{
    public class ErrorPayload : IPayload
    {
        public ErrorCodes Code { get; set; } = ErrorCodes.Undefined;
        public string Message { get; set; } = string.Empty;

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
