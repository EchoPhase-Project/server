// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Types.Result;

namespace EchoPhase.Cli.Commands.Workflows
{
    /// <summary>
    /// Saves workflows the way the API does: validation, package links,
    /// schedules. The server implements it, since its workflow service does
    /// all of that; the commands only ask.
    /// </summary>
    public interface IWorkflowEditor
    {
        /// <summary>Creates an inactive workflow; without a package, in a new package of its own.</summary>
        Task<IServiceResult<Workflow>> CreateInactiveAsync(
            Guid userId, Guid? packageId, string name, BlockProgram program, CancellationToken ct);

        /// <summary>Turns the workflow's triggers on or off; its program stays as it is.</summary>
        Task<IServiceResult<Workflow>> SetActiveAsync(Workflow workflow, bool active, CancellationToken ct);
    }
}
