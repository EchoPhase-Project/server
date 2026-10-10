// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Authentication.ApiClients
{
    public class ApiClientOptions : IValidatable
    {
        private static readonly TimeSpan MinTokenLifetime = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan MaxTokenLifetime = TimeSpan.FromHours(24);

        public const string SectionName = "Authentication:ApiClients";

        /// <summary>
        /// Key-vault entry holding the HMAC key used to hash client secrets.
        /// Separate from the session key so rotating one does not break the other.
        /// Changing or losing it invalidates every client secret.
        /// </summary>
        public string Key { get; set; } = "api-clients";

        /// <summary>
        /// Lifetime of access tokens issued for client credentials. There is no
        /// refresh token: clients simply request a new one with their secret.
        /// </summary>
        public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(30);

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Key))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Key), "Key cannot be empty."));

            if (TokenLifetime < MinTokenLifetime || TokenLifetime > MaxTokenLifetime)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(TokenLifetime),
                        $"TokenLifetime must be between {MinTokenLifetime} and {MaxTokenLifetime}."));

            return ValidationResult.Success();
        }
    }
}
