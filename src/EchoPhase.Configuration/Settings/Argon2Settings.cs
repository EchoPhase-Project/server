namespace EchoPhase.Configuration.Settings
{
    public class Argon2Settings : IValidatable
    {
        public string Key { get; set; } = "argon2";

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Key))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Key), "Key cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
