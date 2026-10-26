// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Cli.Commands.Workflows;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Types.Result;

namespace EchoPhase.Server.Services.Workflows
{
    /// <summary>The CLI's way to save workflows: through <see cref="WorkflowService"/>, with no actor.</summary>
    public class WorkflowEditor : IWorkflowEditor
    {
        private readonly WorkflowService _workflows;

        public WorkflowEditor(WorkflowService workflows)
        {
            _workflows = workflows;
        }

        public async Task<IServiceResult<Workflow>> CreateInactiveAsync(
            Guid userId, Guid? packageId, string name, BlockProgram program, CancellationToken ct)
        {
            var result = await _workflows.CreateAsync(userId, null, packageId, name, program, active: false, ct);
            return result.Map(details => details.Workflow);
        }

        public async Task<IServiceResult<Workflow>> SetActiveAsync(Workflow workflow, bool active, CancellationToken ct)
        {
            var result = await _workflows.UpdateAsync(workflow.UserId, null, workflow.Id, workflow.Name,
                WorkflowService.Parse(workflow), active, workflow.Version, ct);
            return result.Map(details => details.Workflow);
        }
    }
}
