// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockType(BlockTypes.For, Outputs = [BlockOutputs.Done, BlockOutputs.Body],
        Description = "Runs \"body\" once per counter value (its input is the counter), then goes on through \"done\" with the iteration count.")]
    public class ForHandler : BlockHandlerBase<ForParams>
    {
        public override async Task<BlockResult> ExecuteAsync(BlockCall call, ForParams param)
        {
            var context = call.Context;
            int iterations = 0;
            try
            {
                for (int counter = param.Start;
                     param.Step > 0 ? counter <= param.End : counter >= param.End;
                     counter += param.Step)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    BlocksRunner.CountStep(context, call.Block.Id);

                    context.Variables[param.CounterVar] = counter;
                    await call.RunBranchAsync(BlockOutputs.Body, counter);
                    iterations++;
                }
            }
            finally
            {
                context.Variables.Remove(param.CounterVar);
            }

            return BlockResult.To(BlockOutputs.Done, iterations);
        }
    }
}
