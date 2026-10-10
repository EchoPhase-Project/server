// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockType(BlockTypes.While, Outputs = [BlockOutputs.Done, BlockOutputs.Body],
        Description = "Runs \"body\" while the condition holds (its input is the iteration), then goes on through \"done\" with the iteration count.")]
    public class WhileHandler : BlockHandlerBase<WhileParams>
    {
        public override async Task<BlockResult> ExecuteAsync(BlockCall call, WhileParams param)
        {
            var context = call.Context;
            int iterations = 0;

            while (true)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                BlocksRunner.CountStep(context, call.Block.Id);

                // Re-evaluated each time: the body changes the variables.
                if (!Eval.TypedExecute<bool>(param.Condition, call.Scope))
                    break;

                await call.RunBranchAsync(BlockOutputs.Body, iterations);
                iterations++;
            }

            return BlockResult.To(BlockOutputs.Done, iterations);
        }
    }
}
