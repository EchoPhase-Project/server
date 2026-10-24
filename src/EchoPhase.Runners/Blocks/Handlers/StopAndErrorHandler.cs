// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Handlers
{
    // No outputs: the error goes where the block's OnError policy sends it.
    [BlockType(BlockTypes.StopAndError, Outputs = [],
        Description = "Fails on purpose with the given message; the block's onError decides what follows.")]
    public class StopAndErrorHandler : BlockHandlerBase<StopAndErrorParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, StopAndErrorParams param)
            => throw new BlockFailedException(param.Type, Eval.TypedProcess<string>(param.Message, call.Scope));
    }
}
