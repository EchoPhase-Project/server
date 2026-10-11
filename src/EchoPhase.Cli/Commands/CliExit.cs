// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Cli.Commands
{
    /// <summary>
    /// What a command returns: a process exit code, or <see cref="StartServer"/>
    /// to go on and run the server (Program starts it for any negative value).
    /// </summary>
    public static class CliExit
    {
        public const int Success = 0;
        public const int Failure = 1;
        public const int StartServer = -1;
    }
}
