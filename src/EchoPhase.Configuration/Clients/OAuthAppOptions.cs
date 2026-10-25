// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics.CodeAnalysis;

namespace EchoPhase.Configuration.Clients
{
    public class OAuthAppOptions : IValidatable
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Callback URL registered with the provider, e.g.
        /// <c>https://example.com/api/v1/connections/twitch/callback</c>.
        /// User-owned apps must register the same URL.
        /// </summary>
        public string? RedirectUri { get; set; }

        public List<string> Scopes { get; set; } = new();

        [MemberNotNullWhen(true, nameof(ClientId), nameof(ClientSecret))]
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

        public IValidationResult Validate()
        {
            if (!string.IsNullOrWhiteSpace(ClientId) && string.IsNullOrWhiteSpace(ClientSecret))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ClientSecret), "ClientSecret is required when ClientId is set."));

            if (!string.IsNullOrWhiteSpace(RedirectUri) && !Uri.TryCreate(RedirectUri, UriKind.Absolute, out _))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(RedirectUri), "RedirectUri must be an absolute URL."));

            return ValidationResult.Success();
        }
    }
}
