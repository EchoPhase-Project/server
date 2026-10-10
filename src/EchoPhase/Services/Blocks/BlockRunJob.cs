// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Services.Blocks
{
    /// <summary>
    /// A validated program waiting for a worker. <see cref="Run"/> is the
    /// history row already stored as queued; only the worker touches it from here.
    /// </summary>
    public sealed record BlockRunJob(
        BlockRun Run,
        BlockProgram Program,
        IReadOnlyCollection<int> StartIds,
        object? Input,
        CancellationToken Cancelled);
}
