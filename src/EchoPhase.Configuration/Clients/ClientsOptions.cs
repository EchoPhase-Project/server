// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Clients
{
    public class ClientsOptions : IValidatable
    {
        public const string SectionName = "Clients";

        /// <summary>
        /// Where the browser is sent after an OAuth connection attempt.
        /// <c>status</c> and <c>connection</c> are appended as query parameters.
        /// </summary>
        public string ConnectionsReturnUrl { get; set; } = "/";

        /// <summary>
        /// How often stored OAuth connections are re-validated in the background.
        /// Twitch requires apps to validate tokens at least hourly.
        /// </summary>
        public TimeSpan ValidationInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Access tokens expiring within this window are refreshed before use.
        /// </summary>
        public TimeSpan RefreshSkew { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The platform's own Twitch application. Leave <c>ClientId</c> empty to
        /// disable the platform app; manually supplied tokens and user-owned
        /// apps keep working.
        /// </summary>
        public OAuthAppOptions Twitch { get; set; } = new()
        {
            Scopes = ["channel:read:vips"]
        };

        public IValidationResult Validate()
        {
            return Twitch.Validate().WithPrefix(nameof(Twitch))
                .Then(() => ValidateSelf());
        }

        private IValidationResult ValidateSelf()
        {
            if (ValidationInterval < TimeSpan.FromMinutes(1))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ValidationInterval), "ValidationInterval must be at least one minute."));

            if (RefreshSkew < TimeSpan.Zero)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(RefreshSkew), "RefreshSkew cannot be negative."));

            return ValidationResult.Success();
        }
    }
}
