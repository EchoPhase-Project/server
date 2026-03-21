// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Database
{
    public class DatabaseOptions : IValidatable
    {
        public const string SectionName = "Database";
        public Redis.RedisOptions Redis { get; set; } = new();
        public Postgres.PostgresOptions Postgres { get; set; } = new();
        public Scylla.ScyllaOptions Scylla { get; set; } = new();

        public IValidationResult Validate()
        {
            return Redis.Validate().WithPrefix(nameof(Redis))
                .Then(() => Postgres.Validate().WithPrefix(nameof(Postgres)))
                .Then(() => Scylla.Validate().WithPrefix(nameof(Scylla)))
                .Then(() => ValidateSelf());
        }

        private IValidationResult ValidateSelf()
        {
            return ValidationResult.Success();
        }
    }
}
