namespace EchoPhase.Configuration.Settings
{
    public class RoslynRunnerSettings : IValidatable
    {
        public ISet<string>? Import { get; set; } = null;
        public ISet<string>? Allow { get; set; } = null;

        public IValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
