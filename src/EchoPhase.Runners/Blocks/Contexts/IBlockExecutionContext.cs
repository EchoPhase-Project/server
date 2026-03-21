// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks.Contexts
{
    public interface IBlockExecutionContext
    {
        IEnumerable<int> StartIds
        {
            get; set;
        }
        IEnumerable<IBlock> Blocks
        {
            get; set;
        }
        IDictionary<string, object> Variables
        {
            get; set;
        }
        IDictionary<string, int> LoopStates
        {
            get; set;
        }
        IList<string> Output
        {
            get; set;
        }
        IList<BlockError> Errors
        {
            get; set;
        }
    }
}
