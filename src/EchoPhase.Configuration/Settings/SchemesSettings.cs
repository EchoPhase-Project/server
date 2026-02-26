using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Settings
{
    public class SchemesSettings : IValidatable
    {
        public BearerSettings Bearer { get; set; } = new();

        public IValidationResult Validate()
        {
            return Bearer.Validate().WithPrefix(nameof(Bearer))
                .Then(() => ValidateSelf());
        }

        private IValidationResult ValidateSelf()
        {
            return ValidationResult.Success();
        }
    }
}
