namespace EchoPhase.Configuration.Runner.Roslyn
{
    public class RoslynSettings : IValidatable
    {
        public ISet<string>? Import { get; set; } = null;
        public ISet<string>? Allow { get; set; } = null;

        public IValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
