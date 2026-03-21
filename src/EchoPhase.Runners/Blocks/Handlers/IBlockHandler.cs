// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks.Handlers
{
    public interface IBlockHandler
    {
        /// <summary>
        /// The type of parameters this handler processes.
        /// </summary>
        Type ParamType
        {
            get;
        }

        /// <summary>
        /// Executes the block handling with given parameters.
        /// Returns a list of IDs of the next blocks to execute.
        /// </summary>
        /// <param name="context">The execution context for the block.</param>
        /// <param name="block">Current block.</param>
        /// <param name="param">The block parameters as an object (will be deserialized inside the handler).</param>
        /// <returns>A list of IDs for the next blocks to run.</returns>
        Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, object param);
    }

    public interface IBlockHandler<TParams> : IBlockHandler
    {
        /// <summary>
        /// Executes the block handling with given parameters.
        /// Returns a list of IDs of the next blocks to execute.
        /// </summary>
        /// <param name="context">The execution context for the block.</param>
        /// <param name="block">Current block.</param>
        /// <param name="param">The block parameters as an object (will be deserialized inside the handler).</param>
        /// <returns>A list of IDs for the next blocks to run.</returns>
        Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, TParams param);
    }
}
