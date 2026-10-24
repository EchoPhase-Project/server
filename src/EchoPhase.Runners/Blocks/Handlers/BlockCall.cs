// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>Data that reached a block over one edge.</summary>
    public sealed record BlockInput(int From, object? Data);

    /// <summary>One execution of one block, as its handler sees it.</summary>
    public sealed class BlockCall
    {
        private readonly Func<BlockCall, string, object?, Task> _runBranch;

        internal BlockCall(
            IBlockExecutionContext context,
            IBlock block,
            IReadOnlyList<BlockInput> inputs,
            Func<BlockCall, string, object?, Task> runBranch)
        {
            Context = context;
            Block = block;
            Inputs = inputs;
            _runBranch = runBranch;
        }

        public IBlockExecutionContext Context { get; }

        public IBlock Block { get; }

        /// <summary>Everything that arrived; several only for a Merge.</summary>
        public IReadOnlyList<BlockInput> Inputs { get; }

        /// <summary>The (first) input's data; a trigger's is the event payload.</summary>
        public object? Input => Inputs.Count > 0 ? Inputs[0].Data : null;

        private ExpressionScope? _scope;

        /// <summary>
        /// Variables plus input, blocks and errors, for evaluating this block's
        /// expressions. One per call: it reads the run's live state, so it never goes stale.
        /// </summary>
        public IDictionary<string, object> Scope
            => _scope ??= new ExpressionScope(Context.Variables, Input, Context.BlockOutputs, Context.Failures, Context.Settings.TimeZone);

        /// <summary>
        /// Runs the blocks connected to <paramref name="output"/> now, to the
        /// end, before this block finishes: a loop's body. They get
        /// <paramref name="input"/> as their input.
        /// </summary>
        public Task RunBranchAsync(string output, object? input) => _runBranch(this, output, input);
    }

    /// <summary>How a block finished: the output to follow (none: the branch ends) and its data.</summary>
    public readonly record struct BlockResult(string? Output, object? Data)
    {
        public static BlockResult Main(object? data = null) => new(BlockOutputs.Main, data);

        public static BlockResult To(string output, object? data = null) => new(output, data);

        /// <summary>Nothing follows; the data is still the block's output.</summary>
        public static BlockResult End(object? data = null) => new(null, data);
    }
}
