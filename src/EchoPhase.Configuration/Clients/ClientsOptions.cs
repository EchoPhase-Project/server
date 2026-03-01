namespace EchoPhase.Configuration.Clients
{
    public class ClientsOptions : IValidatable
    {
        public const string SectionName = "Clients";

        public IValidationResult Validate()
        {
            return ValidateSelf();
        }

        private IValidationResult ValidateSelf()
        {
            return ValidationResult.Success();
        }
    }
}
