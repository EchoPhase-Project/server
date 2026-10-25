// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Server.Services.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

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

        /// <summary>
        /// The workflow as an export file ({ format, name, version, contentHash,
        /// exportedAt, program }), or one saved version with ?version=N. The
        /// ETag is the program's hash: If-None-Match answers 304 while it is
        /// unchanged.
        /// </summary>
        [HttpGet("{id:guid}/export")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Export(Guid id, int? version, CancellationToken ct = default)
        {
            var result = await _workflows.ExportAsync(User.GetUserId(), id, version, ct);
            if (!result.Successful)
                return Failure(result.Error);

            var export = result.Value;
            var etag = new EntityTagHeaderValue($"\"{export.ContentHash}\"");
            var headers = Response.GetTypedHeaders();
            headers.ETag = etag;
            // Revalidated every time: the name and version in the file may change with the same program.
            headers.CacheControl = new CacheControlHeaderValue { Private = true, NoCache = true };
            if (Request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Compare(etag, useStrongComparison: false)))
                return StatusCode(StatusCodes.Status304NotModified);

            return File(Encoding.UTF8.GetBytes(export.Json), "application/json", export.FileName);
        }

        /// <summary>
        /// A new workflow from an export file sent as the body; inactive until
        /// turned on. `intact` is false when the program does not match the
        /// file's contentHash (it was edited after export).
        /// </summary>
        /// <param name="name">Instead of the name in the file.</param>
        [HttpPost("import")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Create)]
        [RequestSizeLimit(MaxImportBytes)]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Import([FromBody] JsonElement file, [MaxLength(100)] string? name, CancellationToken ct = default)
            => Imported(await _workflows.ImportAsync(User.GetUserId(), User.GetActorId(), file.GetRawText(), name, ct));

        /// <summary>The same, from an uploaded file (multipart/form-data: file, name).</summary>
        [HttpPost("import/file")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Create)]
        [RequestSizeLimit(MaxImportBytes)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ImportFile([FromForm] ImportWorkflowFileRequest dto, CancellationToken ct = default)
        {
            if (dto.File.Length > MaxImportBytes)
                return Error(WorkflowErrors.Invalid, $"An export file is at most {MaxImportBytes} bytes.", StatusCodes.Status400BadRequest);
            using var reader = new StreamReader(dto.File.OpenReadStream(), Encoding.UTF8);
            var json = await reader.ReadToEndAsync(ct);
            return Imported(await _workflows.ImportAsync(User.GetUserId(), User.GetActorId(), json, dto.Name, ct));
        }

        /// <summary>An export file is a program of at most a few hundred blocks: far below this.</summary>
        public const int MaxImportBytes = 1 << 20;

        private IActionResult Imported(Types.Result.IServiceResult<WorkflowImport> result)
            => result.Successful
                ? CreatedAtAction(nameof(Get), new { id = result.Value.Details.Workflow.Id },
                    new { workflow = View(result.Value.Details), intact = result.Value.Intact })
                : Failure(result.Error);

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
                ? Ok(result.Value.Select(v => new
                {
                    version = v.Version,
                    name = v.Name,
                    contentHash = v.ContentHash,
                    parentVersion = v.ParentVersion,
                    restoredFrom = v.RestoredFrom,
                    savedBy = v.SavedBy,
                    createdAt = v.CreatedAt,
                }))
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
            return Ok(new
            {
                version = v.Version,
                name = v.Name,
                contentHash = v.ContentHash ?? BlockProgramHash.Of(result.Value.Program),
                parentVersion = v.ParentVersion,
                restoredFrom = v.RestoredFrom,
                savedBy = v.SavedBy,
                createdAt = v.CreatedAt,
                program = result.Value.Program,
            });
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
            contentHash = w.ContentHash,
            createdAt = w.CreatedAt,
            updatedAt = w.UpdatedAt
        };

        private static object View(WorkflowDetails details) => new
        {
            id = details.Workflow.Id,
            name = details.Workflow.Name,
            active = details.Workflow.Active,
            version = details.Workflow.Version,
            contentHash = details.Workflow.ContentHash ?? BlockProgramHash.Of(details.Program),
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
