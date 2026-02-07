namespace EchoPhase.Configuration.Settings
{
    public class ScyllaSettings : IScyllaSettings, IValidatable
    {
        public string ContactPoint { get; set; } = string.Empty;
        public string Keyspace { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ContactPoint))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ContactPoint), "ContactPoint cannot be empty."));

            if (string.IsNullOrWhiteSpace(Keyspace))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Keyspace), "Keyspace cannot be empty."));

            if (!string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Password), "Password required with username."));

            return ValidationResult.Success();
        }
    }
}
