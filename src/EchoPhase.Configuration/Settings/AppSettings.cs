namespace EchoPhase.Configuration.Settings
{
    public class AppSettings : IValidatable
    {
        public IValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
