// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Database.Scylla
{
    public class ScyllaOptions : IScyllaOptions, IValidatable
    {
        public const string SectionName = "Database:Scylla";
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
