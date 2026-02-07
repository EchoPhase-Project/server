namespace EchoPhase.Configuration.Settings
{
    public class PostgresSettings : IValidatable
    {
        public string ConnectionString { get; set; } = string.Empty;

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ConnectionString), "Postgres connection string is missing."));

            return ValidationResult.Success();
        }
    }
}
