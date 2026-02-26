using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Settings
{
    public class AuthenticationSettings : IValidatable
    {
        public SchemesSettings Schemes { get; set; } = new();

        public IValidationResult Validate()
        {
            return Schemes.Validate().WithPrefix(nameof(Schemes));
        }
    }
}
