// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks
{
    /// <summary>A saved workflow's program and where it lives.</summary>
    public sealed record SavedProgram(Guid WorkflowId, Guid PackageId, BlockProgram Program);

    /// <summary>Saved workflows, for ExecuteWorkflow; the host provides it.</summary>
    public interface IWorkflowSource
    {
        /// <summary>One of the user's workflows, or null.</summary>
        Task<SavedProgram?> FindAsync(Guid userId, Guid workflowId, CancellationToken ct);

        /// <summary>The workflow of a package by its key, or null.</summary>
        Task<SavedProgram?> FindInPackageAsync(Guid userId, Guid packageId, Guid key, CancellationToken ct);
    }
}
