// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Server.Services.Workflows
{
    /// <summary>Saved workflows for ExecuteWorkflow: the owner's only, active or not.</summary>
    public class WorkflowProgramSource : IWorkflowSource
    {
        private readonly WorkflowRepository _workflows;

        public WorkflowProgramSource(WorkflowRepository workflows)
        {
            _workflows = workflows;
        }

        public async Task<BlockProgram?> FindAsync(Guid userId, Guid workflowId, CancellationToken ct)
            => await _workflows.Query().WithIds(workflowId).WithUserIds(userId).FirstOrDefaultAsync(ct) is { } workflow
                ? WorkflowService.Parse(workflow)
                : null;
    }
}
