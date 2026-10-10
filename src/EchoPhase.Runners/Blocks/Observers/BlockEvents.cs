// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks.Observers
{
    /// <param name="Context">The run; read it, do not change it.</param>
    /// <param name="Block">The block.</param>
    /// <param name="Inputs">What reached it.</param>
    /// <param name="Depth">0 for the program, +1 inside each loop body.</param>
    public record BlockEvent(IBlockExecutionContext Context, IBlock Block, IReadOnlyList<BlockInput> Inputs, int Depth);

    /// <param name="Output">The output followed; null: the branch ended here.</param>
    /// <param name="Data">The block's output data.</param>
    /// <param name="Elapsed">Time in the handler, retries and nested branches included.</param>
    /// <param name="Disabled">Skipped: its input passed through.</param>
    public sealed record BlockCompletedEvent(
        IBlockExecutionContext Context, IBlock Block, IReadOnlyList<BlockInput> Inputs, int Depth,
        string? Output, object? Data, TimeSpan Elapsed, bool Disabled)
        : BlockEvent(Context, Block, Inputs, Depth);

    /// <param name="Error">What went wrong.</param>
    /// <param name="Attempt">Which try failed (1-based).</param>
    /// <param name="WillRetry">Another try follows.</param>
    /// <param name="Handled">The block's policy turned the error into output; the run goes on.</param>
    public sealed record BlockFailedEvent(
        IBlockExecutionContext Context, IBlock Block, IReadOnlyList<BlockInput> Inputs, int Depth,
        BlockError Error, int Attempt, bool WillRetry, bool Handled)
        : BlockEvent(Context, Block, Inputs, Depth);
}
