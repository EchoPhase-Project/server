// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Entities
{
    public static class BlockRunStatus
    {
        public const string Queued = "queued";
        public const string Running = "running";
        public const string Succeeded = "succeeded";

        /// <summary>A block failed or a limit was exceeded; see the result's errors.</summary>
        public const string Failed = "failed";

        public const string Cancelled = "cancelled";
        public const string TimedOut = "timedOut";

        /// <summary>The instance stopped before the run finished.</summary>
        public const string Interrupted = "interrupted";

        public static bool IsFinished(string status)
            => status is not (Queued or Running);
    }
}
