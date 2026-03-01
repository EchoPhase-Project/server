using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Cryptography
{
    public class CryptographyOptions : IValidatable
    {
        public const string SectionName = "Cryptography";
        public Aes.AesOptions Aes { get; set; } = new();
        public Crypto25519.Crypto25519Options Crypto22519 { get; set; } = new();

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
