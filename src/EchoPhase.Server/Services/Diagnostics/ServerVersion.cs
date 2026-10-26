// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;

namespace EchoPhase.Server.Services.Diagnostics
{
    /// <summary>What this build calls itself in error reports and telemetry.</summary>
    public static class ServerVersion
    {
        /// <summary>The image tag the server was built as (EchoPhaseVersion); 0.0.0-local for a local build.</summary>
        public static string Current { get; } = typeof(ServerVersion).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "EchoPhaseVersion")?.Value is { Length: > 0 } version
                ? version
                : "0.0.0-local";
    }
}
