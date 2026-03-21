// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Models
{
    public class Block : IBlock
    {
        public int Id
        {
            get; set;
        }
        public BlockTypes Type
        {
            get; set;
        }
        public object Params { get; set; } = new();
    }
}
