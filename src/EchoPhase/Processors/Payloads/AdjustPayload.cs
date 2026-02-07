using EchoPhase.Attributes;
using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Payloads
{
    [OpCodePayload(OpCodes.Adjust)]
    public class AdjustPayload : IPayload
    {
        public string Intents { get; set; } = string.Empty;

        public AdjustPayload()
        {
        }

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Intents))
            {
                errorMessage = "Intents cannot be empty.";
                return false;
            }

            return true;
        }
    }
}
