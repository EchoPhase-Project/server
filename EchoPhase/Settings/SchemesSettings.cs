using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class SchemesSettings : IValidatable
    {
        public BearerSettings Bearer { get; set; } = new();

        public bool IsValid(out string errorMessage)
        {
            if (!Bearer.IsValid(out errorMessage))
                return false;

            errorMessage = string.Empty;
            return true;
        }
    }
}
