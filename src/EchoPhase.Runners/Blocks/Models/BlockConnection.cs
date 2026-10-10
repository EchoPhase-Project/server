// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>
    /// An edge: when <paramref name="From"/> finishes through output
    /// <paramref name="Output"/>, <paramref name="To"/> runs with its output as input.
    /// </summary>
    public sealed record BlockConnection(int From, int To, string Output = BlockOutputs.Main);

    /// <summary>Output names used by more than one type.</summary>
    public static class BlockOutputs
    {
        public const string Main = "main";
        public const string Error = "error";
        public const string True = "true";
        public const string False = "false";
        public const string Body = "body";
        public const string Done = "done";
    }
}
