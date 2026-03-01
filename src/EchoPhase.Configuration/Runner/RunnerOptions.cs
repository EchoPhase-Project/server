using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Runner
{
    public class RunnerOptions : IValidatable
    {
        public const string SectionName = "Runner";
        public Roslyn.RoslynOptions Roslyn { get; set; } = new();

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
