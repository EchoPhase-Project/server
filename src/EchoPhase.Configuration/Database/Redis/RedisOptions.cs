// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Database.Redis
{
    public class RedisOptions : IRedisOptions, IValidatable
    {
        public const string SectionName = "Database:Redis";
        public string ConnectionString { get; set; } = string.Empty;
        public string InstanceName { get; set; } = string.Empty;
        public Guid TenantId
        {
            get; set;
        } = Guid.Empty;

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ConnectionString), "Redis connection string is missing."));

            if (string.IsNullOrWhiteSpace(InstanceName))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(InstanceName), "Redis instance name is missing."));

            if (TenantId == Guid.Empty)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(TenantId), "TenantId cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
