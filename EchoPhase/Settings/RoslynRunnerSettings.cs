using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class RoslynRunnerSettings : IValidatable
    {
        public ISet<string>? Import { get; set; } = null;
        public ISet<string>? Allow { get; set; } = null;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;
            return true;
        }
    }
}
