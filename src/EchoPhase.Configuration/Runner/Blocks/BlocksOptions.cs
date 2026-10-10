// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Runner.Blocks
{
    /// <summary>
    /// Limits for user-submitted block programs and the background queue that
    /// runs them. Every limit exists because a block graph is untrusted input.
    /// </summary>
    public class BlocksOptions : IValidatable
    {
        public const string SectionName = "Runner:Blocks";

        /// <summary>Blocks in one submitted graph.</summary>
        public int MaxBlocks { get; set; } = 200;

        /// <summary>Executed blocks per run, loop iterations included.</summary>
        public int MaxSteps { get; set; } = 10_000;

        /// <summary>Entries a run may print.</summary>
        public int MaxOutputEntries { get; set; } = 1_000;

        /// <summary>Serialized size of a run's result (output, variables, errors).</summary>
        public int MaxResultBytes { get; set; } = 256 * 1024;

        /// <summary>Wall-clock limit of one run.</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Runs executing in parallel on this instance.</summary>
        public int Workers { get; set; } = 4;

        /// <summary>Runs waiting for a worker before new ones are rejected.</summary>
        public int QueueCapacity { get; set; } = 100;

        /// <summary>Queued plus running runs one user may have.</summary>
        public int MaxActivePerUser { get; set; } = 3;

        /// <summary>How long a run's full result can be fetched.</summary>
        public TimeSpan ResultTtl { get; set; } = TimeSpan.FromDays(1);

        /// <summary>How long a run stays in the user's history.</summary>
        public TimeSpan HistoryTtl { get; set; } = TimeSpan.FromDays(90);

        public IValidationResult Validate()
        {
            return Positive(MaxBlocks, nameof(MaxBlocks))
                .Then(() => Positive(MaxSteps, nameof(MaxSteps)))
                .Then(() => Positive(MaxOutputEntries, nameof(MaxOutputEntries)))
                .Then(() => Positive(MaxResultBytes, nameof(MaxResultBytes)))
                .Then(() => Positive(Workers, nameof(Workers)))
                .Then(() => Positive(QueueCapacity, nameof(QueueCapacity)))
                .Then(() => Positive(MaxActivePerUser, nameof(MaxActivePerUser)))
                .Then(() => Within(Timeout, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(10), nameof(Timeout)))
                .Then(() => Within(ResultTtl, TimeSpan.FromMinutes(1), HistoryTtl, nameof(ResultTtl)))
                // Scylla caps TTLs at 20 years; a year is plenty for run history.
                .Then(() => Within(HistoryTtl, TimeSpan.FromHours(1), TimeSpan.FromDays(365), nameof(HistoryTtl)));
        }

        private static IValidationResult Positive(int value, string name)
            => value > 0
                ? ValidationResult.Success()
                : ValidationResult.Failure(error => error.Set(name, $"{name} must be positive."));

        private static IValidationResult Within(TimeSpan value, TimeSpan min, TimeSpan max, string name)
            => value >= min && value <= max
                ? ValidationResult.Success()
                : ValidationResult.Failure(error => error.Set(name, $"{name} must be between {min} and {max}."));
    }
}
