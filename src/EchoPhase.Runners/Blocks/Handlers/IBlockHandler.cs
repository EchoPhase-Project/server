// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Handlers
{
    public interface IBlockHandler
    {
        Type ParamType { get; }

        /// <summary>Turns raw params (JSON from the program) into the handler's type and checks them.</summary>
        object Bind(object? param);

        /// <summary>Runs the block with params from <see cref="Bind"/>.</summary>
        Task<BlockResult> ExecuteAsync(BlockCall call, object param);
    }
}
