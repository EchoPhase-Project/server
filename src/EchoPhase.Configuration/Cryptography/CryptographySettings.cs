using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Cryptography
{
    public class CryptographySettings : IValidatable
    {
        public Aes.AesSettings Aes { get; set; } = new();
        public Crypto25519.Crypto25519Settings Crypto22519 { get; set; } = new();

        public IValidationResult Validate()
        {
            return Aes.Validate().WithPrefix(nameof(Aes))
                .Then(() => Crypto22519.Validate().WithPrefix(nameof(Crypto22519)))
                .Then(() => ValidateSelf());
        }

        private IValidationResult ValidateSelf()
        {
            return ValidationResult.Success();
        }
    }
}
