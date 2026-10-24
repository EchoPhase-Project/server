// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks
{
    /// <summary>Saved workflows, for ExecuteWorkflow; the host provides it.</summary>
    public interface IWorkflowSource
    {
        /// <summary>The program of one of the user's workflows, or null.</summary>
        Task<BlockProgram?> FindAsync(Guid userId, Guid workflowId, CancellationToken ct);
    }
}
