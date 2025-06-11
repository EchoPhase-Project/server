using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class AppSettings : IValidatable
    {
        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;
            return true;
        }
    }
}
