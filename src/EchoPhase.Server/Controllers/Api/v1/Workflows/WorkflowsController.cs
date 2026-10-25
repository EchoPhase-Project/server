// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Runners.Blocks;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Services.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Workflows
{
    /// <summary>
    /// Saved block programs. Runs of them are listed and fetched with the
    /// other runs (api/v1/blocks/runs); their webhooks are api/v1/hooks.
    /// </summary>
    [Route("api/v1/workflows")]
    public class WorkflowsController : ApiControllerBase
    {
        private readonly WorkflowService _workflows;

        public WorkflowsController(WorkflowService workflows)
        {
            _workflows = workflows;
        }

        [HttpGet]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        public async Task<IActionResult> List(CancellationToken ct = default)
        {
            var workflows = await _workflows.ListAsync(User.GetUserId(), ct);
            return Ok(workflows.Select(Summary));
        }

        [HttpGet("{id:guid}")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
        {
            var result = await _workflows.GetAsync(User.GetUserId(), id, ct);
            return result.Successful ? Ok(View(result.Value)) : Failure(result.Error);
        }

        [HttpPost]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Create)]
        public async Task<IActionResult> Create([FromBody] SaveWorkflowRequest dto, CancellationToken ct = default)
        {
            var result = await _workflows.CreateAsync(User.GetUserId(), User.GetActorId(), dto.Name, dto.Program, dto.Active, ct);
            return result.Successful
                ? CreatedAtAction(nameof(Get), new { id = result.Value.Workflow.Id }, View(result.Value))
                : Failure(result.Error);
        }

        /// <summary>Replaces name, program and active flag; send the version you edited to avoid lost updates.</summary>
        [HttpPut("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Update)]
        public async Task<IActionResult> Update(Guid id, [FromBody] SaveWorkflowRequest dto, CancellationToken ct = default)
        {
            var result = await _workflows.UpdateAsync(User.GetUserId(), User.GetActorId(), id, dto.Name, dto.Program, dto.Active, dto.Version, ct);
            return result.Successful ? Ok(View(result.Value)) : Failure(result.Error);
        }

        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Delete)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var result = await _workflows.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        /// <summary>Runs it now (active or not). 202 with the run id, as api/v1/blocks/runs.</summary>
        [HttpPost("{id:guid}/runs")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Execute)]
        public async Task<IActionResult> Run(Guid id, [FromBody] RunWorkflowRequest? dto, CancellationToken ct = default)
        {
            var result = await _workflows.RunAsync(User.GetUserId(), User.GetActorId(), id, dto?.Input, ct);
            if (!result.Successful)
                return Failure(result.Error);

            var runId = result.Value.RunId;
            return Accepted($"/api/v1/blocks/runs/{runId}", new { runId, status = BlockRunStatus.Queued });
        }

        /// <summary>Saved versions, newest first: one per change of the program, the last 50.</summary>
        [HttpGet("{id:guid}/versions")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        public async Task<IActionResult> Versions(Guid id, CancellationToken ct = default)
        {
            var result = await _workflows.VersionsAsync(User.GetUserId(), id, ct);
            return result.Successful
                ? Ok(result.Value.Select(v => new { version = v.Version, name = v.Name, savedBy = v.SavedBy, createdAt = v.CreatedAt }))
                : Failure(result.Error);
        }

        [HttpGet("{id:guid}/versions/{version:int}")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        public async Task<IActionResult> Version(Guid id, int version, CancellationToken ct = default)
        {
            var result = await _workflows.VersionAsync(User.GetUserId(), id, version, ct);
            if (!result.Successful)
                return Failure(result.Error);

            var v = result.Value.Version;
            return Ok(new { version = v.Version, name = v.Name, savedBy = v.SavedBy, createdAt = v.CreatedAt, program = result.Value.Program });
        }

        /// <summary>
        /// Makes that version current again as a new version (history is kept).
        /// Send the current version in the body to avoid overwriting a newer edit.
        /// </summary>
        [HttpPost("{id:guid}/versions/{version:int}/restore")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Update)]
        public async Task<IActionResult> Restore(Guid id, int version, [FromBody] RestoreWorkflowRequest? dto, CancellationToken ct = default)
        {
            var result = await _workflows.RestoreAsync(User.GetUserId(), User.GetActorId(), id, version, dto?.CurrentVersion, ct);
            return result.Successful ? Ok(View(result.Value)) : Failure(result.Error);
        }

        private static object Summary(Workflow w) => new
        {
            id = w.Id,
            name = w.Name,
            active = w.Active,
            version = w.Version,
            createdAt = w.CreatedAt,
            updatedAt = w.UpdatedAt
        };

        private static object View(WorkflowDetails details) => new
        {
            id = details.Workflow.Id,
            name = details.Workflow.Name,
            active = details.Workflow.Active,
            version = details.Workflow.Version,
            createdAt = details.Workflow.CreatedAt,
            updatedAt = details.Workflow.UpdatedAt,
            program = details.Program,
            // Where each WebhookTrigger listens (POST, X-Webhook-Secret header).
            webhooks = details.Program.Blocks
                .Where(b => b.Type == BlockTypes.WebhookTrigger)
                .Select(b => new { blockId = b.Id, path = $"/api/v1/hooks/{details.Workflow.Id}/{b.Id}" }),
            schedules = details.Workflow.Schedules
                .Select(s => new { blockId = s.BlockId, cron = s.Cron, timeZone = s.TimeZone, nextRunAt = s.NextRunAt })
        };
    }
}
