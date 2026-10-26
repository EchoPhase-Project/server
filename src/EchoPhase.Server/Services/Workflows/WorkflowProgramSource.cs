// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Runners.Blocks;

namespace EchoPhase.Server.Services.Workflows
{
    /// <summary>Saved workflows for ExecuteWorkflow and retries: the owner's only, active or not.</summary>
    public class WorkflowProgramSource : IWorkflowSource
    {
        private readonly WorkflowRepository _workflows;

        public WorkflowProgramSource(WorkflowRepository workflows)
        {
            _workflows = workflows;
        }

        public async Task<SavedProgram?> FindAsync(Guid userId, Guid workflowId, CancellationToken ct)
            => Saved(await _workflows.Query().WithIds(workflowId).WithUserIds(userId).FirstOrDefaultAsync(ct));

        public async Task<SavedProgram?> FindInPackageAsync(Guid userId, Guid packageId, Guid key, CancellationToken ct)
            => Saved(await _workflows.Query().WithUserIds(userId).WithPackageIds(packageId).WithKeys(key).FirstOrDefaultAsync(ct));

        private static SavedProgram? Saved(Workflow? workflow)
            => workflow is null ? null : new SavedProgram(workflow.Id, workflow.PackageId, WorkflowService.Parse(workflow));
    }
}
