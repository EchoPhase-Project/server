// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Models
{
    public interface IBlock
    {
        int Id
        {
            get; set;
        }
        BlockTypes Type
        {
            get; set;
        }
        object Params
        {
            get; set;
        }
    }
}
