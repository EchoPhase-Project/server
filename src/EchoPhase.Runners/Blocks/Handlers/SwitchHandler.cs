// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockType(BlockTypes.Switch, Outputs = [BlockOutputs.Fallback],
        Description = "Goes on through the output of the first rule that is true, else \"fallback\"; its input passes through.")]
    public class SwitchHandler : BlockHandlerBase<SwitchParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, SwitchParams param)
        {
            var scope = call.Scope;
            var outputs = param.ExtraOutputs;
            for (int i = 0; i < param.Rules.Count; i++)
                if (Eval.TypedExecute<bool>(param.Rules[i].Condition, scope))
                    return Task.FromResult(BlockResult.To(outputs[i], call.Input));

            return Task.FromResult(BlockResult.To(BlockOutputs.Fallback, call.Input));
        }
    }
}
