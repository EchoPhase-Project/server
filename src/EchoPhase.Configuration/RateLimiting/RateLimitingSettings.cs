// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.RateLimiting
{
    public class RateLimitingSettings : IValidatable
    {
        public const string SectionName = "RateLimiting";

        /// <summary>Set to <c>false</c> to turn all limits off (e.g. load tests).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Anonymous credential endpoints (login, register, refresh, token, OAuth),
        /// per client IP, sliding window.
        /// </summary>
        public AuthLimit Auth { get; set; } = new();

        /// <summary>
        /// Everything else, per API client, impersonation, user or IP, token bucket.
        /// </summary>
        public GlobalLimit Global { get; set; } = new();

        public IValidationResult Validate()
        {
            if (Auth.PermitLimit < 1 || Auth.Window <= TimeSpan.Zero || Auth.SegmentsPerWindow < 1)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Auth), "PermitLimit and SegmentsPerWindow must be positive, Window above zero."));

            if (Global.TokenLimit < 1 || Global.TokensPerPeriod < 1 || Global.ReplenishmentPeriod <= TimeSpan.Zero)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Global), "TokenLimit and TokensPerPeriod must be positive, ReplenishmentPeriod above zero."));

            return ValidationResult.Success();
        }

        public class AuthLimit
        {
            public int PermitLimit { get; set; } = 10;
            public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
            public int SegmentsPerWindow { get; set; } = 6;
        }

        public class GlobalLimit
        {
            /// <summary>Burst size.</summary>
            public int TokenLimit { get; set; } = 100;

            /// <summary>Sustained rate: this many requests per <see cref="ReplenishmentPeriod"/>.</summary>
            public int TokensPerPeriod { get; set; } = 50;

            public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(10);
        }
    }

    public class RateLimitingValidator : ValidatorBase<RateLimitingSettings>
    {
    }
}
