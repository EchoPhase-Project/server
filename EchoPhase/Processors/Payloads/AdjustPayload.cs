using EchoPhase.Interfaces;

namespace EchoPhase.Processors.Payloads
{
    public class AdjustPayload : IPayload
    {
        public long Intents
        {
            get; set;
        }

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (Intents < 0)
            {
                errorMessage = "Intents cannot be negative.";
                return false;
            }

            return true;
        }
    }
}
