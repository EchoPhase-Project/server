// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Database.Scylla
{
    public class ScyllaOptions : IScyllaOptions, IValidatable
    {
        public const string SectionName = "Database:Scylla";

        private static readonly string[] ConsistencyLevels =
        [
            "Any", "One", "Two", "Three", "Quorum", "All",
            "LocalQuorum", "EachQuorum", "LocalOne"
        ];

        private static readonly string[] SerialConsistencyLevels = ["Serial", "LocalSerial"];

        public string ContactPoint { get; set; } = string.Empty;
        public int Port { get; set; } = 9042;
        public string? LocalDatacenter { get; set; }
        public string Keyspace { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Consistency { get; set; } = "LocalOne";
        public string SerialConsistency { get; set; } = "LocalSerial";
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(12);
        public bool AutoCreateKeyspace { get; set; }
        public int ReplicationFactor { get; set; } = 1;

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ContactPoint))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ContactPoint), "ContactPoint cannot be empty."));

            if (Port is < 1 or > 65535)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Port), "Port must be between 1 and 65535."));

            if (string.IsNullOrWhiteSpace(Keyspace))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Keyspace), "Keyspace cannot be empty."));

            if (!string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Password), "Password required with username."));

            if (!ConsistencyLevels.Contains(Consistency, StringComparer.OrdinalIgnoreCase))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Consistency), $"Consistency must be one of: {string.Join(", ", ConsistencyLevels)}."));

            if (!SerialConsistencyLevels.Contains(SerialConsistency, StringComparer.OrdinalIgnoreCase))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(SerialConsistency), "SerialConsistency must be Serial or LocalSerial."));

            if (RequestTimeout <= TimeSpan.Zero)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(RequestTimeout), "RequestTimeout must be positive."));

            if (ReplicationFactor < 1)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ReplicationFactor), "ReplicationFactor must be at least 1."));

            return ValidationResult.Success();
        }
    }
}
