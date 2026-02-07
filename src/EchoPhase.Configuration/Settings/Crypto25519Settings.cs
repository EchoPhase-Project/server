namespace EchoPhase.Configuration.Settings
{
    public class Crypto25519Settings : IValidatable
    {
        public AeadChoice AeadChoice { get; set; } = AeadChoice.ChaCha20Poly1305;

        public IValidationResult Validate()
        {
            if (!Enum.IsDefined(typeof(AeadChoice), AeadChoice))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(AeadChoice), $"Invalid value for {nameof(AeadChoice)}: {AeadChoice}"));

            return ValidationResult.Success();
        }
    }
}
