// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.RegularExpressions;

namespace EchoPhase.Server.Services.Diagnostics
{
    /// <summary>What this server is, for clients to check they fit it.</summary>
    public static partial class ServerVersion
    {
        /// <summary>The image tag the server was built as (EchoPhaseVersion); 0.0.0-local for a local build.</summary>
        public static string Current { get; } = typeof(ServerVersion).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "EchoPhaseVersion")?.Value is { Length: > 0 } version
                ? version
                : "0.0.0-local";

        public static string? Api { get; } = SeriesOf(Current);

        /// <summary>
        /// "major.minor" of the first x.y.z in a version ("dev-0.6.0-3-g…" too);
        /// null when there is none, or for 0.0.x (a build that was never released).
        /// </summary>
        public static string? SeriesOf(string version)
        {
            var match = Release().Match(version);
            if (!match.Success)
                return null;
            var (major, minor) = (match.Groups[1].Value, match.Groups[2].Value);
            return major == "0" && minor == "0" ? null : $"{major}.{minor}";
        }

        [GeneratedRegex(@"(?<![0-9])([0-9]+)\.([0-9]+)\.[0-9]+")]
        private static partial Regex Release();
    }
}
