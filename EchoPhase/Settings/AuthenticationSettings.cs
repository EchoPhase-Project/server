using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class AuthenticationSettings : IValidatable
    {
        public SchemesSettings Schemes { get; set; }= new();

        public bool IsValid(out string errorMessage)
        {
            if (!Schemes.IsValid(out errorMessage))
                return false;

            errorMessage = string.Empty;
            return true;
        }
    }
}
