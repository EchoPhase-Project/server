namespace EchoPhase.Configuration.Runner.Roslyn
{
    public class RoslynOptions : IValidatable
    {
        public const string SectionName = "Runner:Roslyn";
        public ISet<string>? Import { get; set; } = null;
        public ISet<string>? Allow { get; set; } = null;

        public IValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
