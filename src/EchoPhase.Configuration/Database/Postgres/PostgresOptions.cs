namespace EchoPhase.Configuration.Database.Postgres
{
    public class PostgresOptions : IValidatable
    {
        public const string SectionName = "Database:Postgres";
        public string ConnectionString { get; set; } = string.Empty;
        public string Schema { get; set; } = "public";

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ConnectionString), "Postgres connection string is missing."));

            if (string.IsNullOrWhiteSpace(Schema))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Schema), "Postgres schema string is missing."));

            return ValidationResult.Success();
        }
    }
}
