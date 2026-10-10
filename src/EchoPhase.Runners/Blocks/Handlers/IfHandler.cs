// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockType(BlockTypes.If, Outputs = [BlockOutputs.True, BlockOutputs.False],
        Description = "Goes on through \"true\" or \"false\"; its input passes through.")]
    public class IfHandler : BlockHandlerBase<IfParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, IfParams param)
        {
            var flag = Eval.TypedExecute<bool>(param.Condition, call.Scope);
            return Task.FromResult(BlockResult.To(flag ? BlockOutputs.True : BlockOutputs.False, call.Input));
        }
    }
}
