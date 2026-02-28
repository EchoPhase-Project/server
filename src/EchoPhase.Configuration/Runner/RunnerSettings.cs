using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Runner
{
    public class RunnerSettings : IValidatable
    {
        public Roslyn.RoslynSettings Roslyn { get; set; } = new();

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
