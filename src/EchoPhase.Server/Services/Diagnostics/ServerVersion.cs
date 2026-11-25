// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Server.Services.Diagnostics
{
    /// <summary>What this build calls itself in error reports and telemetry.</summary>
    public static class ServerVersion
    {
        /// <summary>
        /// The variable the image sets to its tag. Not baked into the build: a
        /// new tag then rebuilds only the image's last layer, not the publish.
        /// </summary>
        public const string Variable = "ECHOPHASE_VERSION";

        /// <summary>The image tag the server runs as; 0.0.0-local outside an image.</summary>
        public static string Current { get; } = Of(Environment.GetEnvironmentVariable(Variable));

        /// <summary>The version a value of <see cref="Variable"/> names: empty is a local build.</summary>
        public static string Of(string? value)
            => string.IsNullOrWhiteSpace(value) ? "0.0.0-local" : value.Trim();
    }
}
