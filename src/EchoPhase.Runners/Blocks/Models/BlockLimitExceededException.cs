// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>
    /// Ends the whole run, not just the current block: loop handlers do not
    /// catch it, so it unwinds through nested runs to <see cref="BlocksRunner.RunAsync"/>.
    /// </summary>
    public sealed class BlockLimitExceededException : Exception
    {
        public int BlockId { get; }

        public BlockLimitExceededException(int blockId, string message) : base(message)
        {
            BlockId = blockId;
        }
    }
}
