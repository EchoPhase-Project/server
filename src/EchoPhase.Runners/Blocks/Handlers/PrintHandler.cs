// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockType(BlockTypes.Print, Description = "Adds a line to the run's output; its output is the text.")]
    public class PrintHandler : BlockHandlerBase<PrintParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, PrintParams param)
        {
            var output = Eval.TypedProcess<string>(param.Text, call.Scope);
            call.Context.Output.Add(output);
            return Task.FromResult(BlockResult.Main(output));
        }
    }
}
