// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>
    /// Declares a handler: the block type and version it runs, and the
    /// outputs it can finish through (the first one is where a disabled or
    /// "continue on error" block goes). Every block may also have an "error"
    /// output when its policy is ErrorOutput.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BlockTypeAttribute : Attribute
    {
        public BlockTypeAttribute(BlockTypes type)
        {
            Type = type;
        }

        public BlockTypes Type { get; }

        public int Version { get; init; } = 1;

        public string[] Outputs { get; init; } = [Models.BlockOutputs.Main];

        /// <summary>Starts runs from an event; may not have inputs.</summary>
        public bool IsTrigger { get; init; }

        public string Description { get; init; } = string.Empty;
    }
}
