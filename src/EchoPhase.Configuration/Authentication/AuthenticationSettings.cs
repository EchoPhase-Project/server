using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Authentication
{
    public class AuthenticationSettings : IValidatable
    {
        public Bearer.BearerSettings Bearer { get; set; } = new();

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
