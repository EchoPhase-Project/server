// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Database.Postgres
{
    public class PostgresOptions : IValidatable
    {
        public const string SectionName = "Database:Postgres";
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
