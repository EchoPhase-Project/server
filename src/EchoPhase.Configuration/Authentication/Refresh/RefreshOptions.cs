// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Authentication.Refresh
{
    public class RefreshOptions : IValidatable
    {
        private const int MinLifetimeMinutes = 5;
        private const int MaxReuseGraceSeconds = 300;

        public const string SectionName = "Authentication:Refresh";
        /// <summary>
        /// Key-vault entry holding the HMAC key used to hash refresh tokens.
        /// Changing or losing it invalidates every session.
        /// </summary>
        public string Key { get; set; } = "refresh";
        public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(120);

        /// <summary>
        /// How long the refresh token that was just rotated away from is still
        /// accepted, so parallel requests that all hit an expired access token
        /// do not trip reuse detection. Such requests get an access token only.
        /// </summary>
        public TimeSpan ReuseGracePeriod { get; set; } = TimeSpan.FromSeconds(30);

        public IValidationResult Validate()
        {
            if (Lifetime.TotalMinutes < MinLifetimeMinutes)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Lifetime),
                        $"Lifetime must be at least {MinLifetimeMinutes} minutes."));

            if (ReuseGracePeriod < TimeSpan.Zero || ReuseGracePeriod.TotalSeconds > MaxReuseGraceSeconds)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ReuseGracePeriod),
                        $"ReuseGracePeriod must be between 0 and {MaxReuseGraceSeconds} seconds."));

            return ValidationResult.Success();
        }
    }
}
