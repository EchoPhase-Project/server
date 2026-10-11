// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Server
{
    /// <summary>
    /// Entry point for <c>dotnet swagger tofile</c> (found by name). Builds the
    /// host without running it, so <see cref="Program.Main"/> and its CLI never
    /// see the tool's arguments. Building connects to nothing: databases are
    /// opened on first use.
    /// </summary>
    public static class SwaggerHostFactory
    {
        public static IHost CreateHost() => Program.CreateHostBuilder([]).Build();
    }
}
