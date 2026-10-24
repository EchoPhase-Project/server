// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Runner.Blocks
{
    /// <summary>
    /// Limits for user-submitted block programs and the background queue that
    /// runs them. Every limit exists because a block graph is untrusted input.
    /// </summary>
    /// <summary>
    /// Outgoing requests of programs. They are untrusted: by default they may
    /// reach only public addresses (no localhost, private networks or cloud
    /// metadata), checked on every connection, redirects included.
    /// </summary>
    public class BlocksStorageOptions
    {
        /// <summary>One value as JSON; a Bloom filter of 1M bits is about 175 KB.</summary>
        public int MaxValueBytes { get; set; } = 256 * 1024;

        /// <summary>Keys one user keeps, all workflows and the shared scope together.</summary>
        public int MaxEntriesPerUser { get; set; } = 10_000;

        /// <summary>Bytes of values one user keeps.</summary>
        public long MaxBytesPerUser { get; set; } = 50L * 1024 * 1024;
    }

    public class BlocksHttpOptions
    {
        /// <summary>Longest a request may take; a block may ask for less.</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        public int MaxResponseBytes { get; set; } = 5 * 1024 * 1024;

        public int MaxRequestBytes { get; set; } = 1024 * 1024;

        public int MaxRedirects { get; set; } = 5;

        /// <summary>Only for development and tests: lets programs reach private addresses.</summary>
        public bool AllowPrivateNetworks { get; set; }
    }

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

        /// <summary>The HttpRequest block.</summary>
        public BlocksHttpOptions Http { get; set; } = new();

        /// <summary>The Storage block: values kept between runs.</summary>
        public BlocksStorageOptions Storage { get; set; } = new();

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
                .Then(() => Within(HistoryTtl, TimeSpan.FromHours(1), TimeSpan.FromDays(365), nameof(HistoryTtl)))
                .Then(() => Within(Http.Timeout, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), "Http.Timeout"))
                .Then(() => Positive(Http.MaxResponseBytes, "Http.MaxResponseBytes"))
                .Then(() => Positive(Http.MaxRequestBytes, "Http.MaxRequestBytes"))
                .Then(() => Positive(Storage.MaxValueBytes, "Storage.MaxValueBytes"))
                .Then(() => Positive(Storage.MaxEntriesPerUser, "Storage.MaxEntriesPerUser"))
                .Then(() => Storage.MaxBytesPerUser >= Storage.MaxValueBytes
                    ? ValidationResult.Success()
                    : ValidationResult.Failure(error => error.Set("Storage.MaxBytesPerUser",
                        "Storage.MaxBytesPerUser must hold at least one value of Storage.MaxValueBytes.")));
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
