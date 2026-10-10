// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Network
{
    /// <summary>
    /// Which proxies may tell the API the client's real IP and scheme
    /// (<c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c>).
    /// </summary>
    public class ForwardedHeadersSettings : IValidatable
    {
        public const string SectionName = "ForwardedHeaders";

        /// <summary>
        /// Proxies in front of the API: ingress + nginx. Default: 2.
        /// </summary>
        public int ForwardLimit { get; set; } = 2;

        /// <summary>
        /// Networks the proxies run in. Headers from any other address are
        /// ignored, so clients cannot spoof their IP. The defaults trust all
        /// private ranges and loopback, which is right when the API is reachable
        /// only through the proxy; set the cluster's exact CIDR in production.
        /// </summary>
        public List<string> KnownNetworks { get; set; } =
        [
            "10.0.0.0/8",
            "172.16.0.0/12",
            "192.168.0.0/16",
            "127.0.0.0/8",
            "::1/128"
        ];

        public IValidationResult Validate()
        {
            if (ForwardLimit < 1)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ForwardLimit), "ForwardLimit must be at least 1."));

            var invalid = KnownNetworks.Where(n => !System.Net.IPNetwork.TryParse(n, out _)).ToArray();
            if (invalid.Length > 0)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(KnownNetworks), $"Invalid CIDR: {string.Join(", ", invalid)}."));

            return ValidationResult.Success();
        }
    }

    public class ForwardedHeadersValidator : ValidatorBase<ForwardedHeadersSettings>
    {
    }
}
