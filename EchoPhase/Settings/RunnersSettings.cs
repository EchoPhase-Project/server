using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class RunnersSettings : IValidatable
    {
        public RoslynRunnerSettings Roslyn { get; set; } = new();

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!Roslyn.IsValid(out errorMessage))
            {
                return false;
            }

            return true;
        }
    }
}
