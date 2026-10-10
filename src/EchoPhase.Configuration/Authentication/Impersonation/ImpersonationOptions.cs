// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Authentication.Impersonation
{
    public class ImpersonationOptions : IValidatable
    {
        private static readonly TimeSpan MinTokenLifetime = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan MaxTokenLifetime = TimeSpan.FromHours(1);

        public const string SectionName = "Authentication:Impersonation";

        /// <summary>
        /// How long an impersonation lasts. There is no refresh: start a new
        /// one (with a new reason) when it expires.
        /// </summary>
        public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

        public IValidationResult Validate()
        {
            if (TokenLifetime < MinTokenLifetime || TokenLifetime > MaxTokenLifetime)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(TokenLifetime),
                        $"TokenLifetime must be between {MinTokenLifetime} and {MaxTokenLifetime}."));

            return ValidationResult.Success();
        }
    }
}
