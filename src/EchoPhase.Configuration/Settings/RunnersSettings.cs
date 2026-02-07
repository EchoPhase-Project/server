using EchoPhase.Types.Extensions;

namespace EchoPhase.Configuration.Settings
{
    public class RunnersSettings : IValidatable
    {
        public RoslynRunnerSettings Roslyn { get; set; } = new();

        public IValidationResult Validate()
        {
            return Roslyn.Validate().WithPrefix(nameof(Roslyn))
                .Then(() => ValidateSelf());
        }

        private IValidationResult ValidateSelf()
        {
            return ValidationResult.Success();
        }
    }
}
