// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Handlers
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BlockTypeHandlerAttribute : Attribute
    {
        /// <summary>
        /// The type of block this handler is associated with.
        /// </summary>
        public BlockTypes Type
        {
            get;
        }

        public BlockTypeHandlerAttribute(BlockTypes type)
        {
            Type = type;
        }
    }
}
