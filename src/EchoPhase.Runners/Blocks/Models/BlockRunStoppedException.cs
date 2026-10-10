// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>
    /// A block failed with the Stop policy: unwinds every branch and loop to
    /// <see cref="BlocksRunner.RunAsync"/>. The error is already recorded.
    /// </summary>
    public sealed class BlockRunStoppedException(int blockId)
        : Exception($"Block {blockId} failed and stopped the run.")
    {
        public int BlockId { get; } = blockId;
    }
}
