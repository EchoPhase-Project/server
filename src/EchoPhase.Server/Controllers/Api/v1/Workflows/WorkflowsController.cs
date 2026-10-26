// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Blocks;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Server.Services.Blocks;
using EchoPhase.Server.Services.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace EchoPhase.Server.Controllers.Api.v1.Workflows
{
    /// <summary>
    /// Saved block programs. A workflow's runs are listed here
    /// ({id}/runs) and fetched with the other runs (api/v1/blocks/runs);
    /// their webhooks are api/v1/hooks.
    /// </summary>
    [Route("api/v1/workflows")]
    public class WorkflowsController : ApiControllerBase
    {
        private readonly WorkflowService _workflows;
        private readonly BlockRunService _runs;

        public WorkflowsController(WorkflowService workflows, BlockRunService runs)
        {
            _workflows = workflows;
            _runs = runs;
        }

        [HttpGet]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<WorkflowSummaryResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List(CancellationToken ct = default)
        {
            var workflows = await _workflows.ListAsync(User.GetUserId(), ct);
            return Ok(workflows.Select(WorkflowSummaryResponse.From));
        }

        [HttpGet("{id:guid}")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<WorkflowResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
        {
            var result = await _workflows.GetAsync(User.GetUserId(), id, ct);
            return result.Successful ? Ok(WorkflowResponse.From(result.Value)) : Failure(result.Error);
        }

        [HttpPost]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Create)]
        [ProducesResponseType<WorkflowResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] SaveWorkflowRequest dto, CancellationToken ct = default)
        {
            var result = await _workflows.CreateAsync(User.GetUserId(), User.GetActorId(), dto.Name, dto.Program, dto.Active, ct);
            return result.Successful
                ? CreatedAtAction(nameof(Get), new { id = result.Value.Workflow.Id }, WorkflowResponse.From(result.Value))
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
        [ProducesResponseType<WorkflowImportResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Import([FromBody] JsonElement file, [MaxLength(100)] string? name, CancellationToken ct = default)
            => Imported(await _workflows.ImportAsync(User.GetUserId(), User.GetActorId(), file.GetRawText(), name, ct));

        /// <summary>The same, from an uploaded file (multipart/form-data: file, name).</summary>
        [HttpPost("import/file")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Create)]
        [RequestSizeLimit(MaxImportBytes)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType<WorkflowImportResponse>(StatusCodes.Status201Created)]
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
                    new WorkflowImportResponse(WorkflowResponse.From(result.Value.Details), result.Value.Intact))
                : Failure(result.Error);

        /// <summary>Replaces name, program and active flag; send the version you edited to avoid lost updates.</summary>
        [HttpPut("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Update)]
        [ProducesResponseType<WorkflowResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(Guid id, [FromBody] SaveWorkflowRequest dto, CancellationToken ct = default)
        {
            var result = await _workflows.UpdateAsync(User.GetUserId(), User.GetActorId(), id, dto.Name, dto.Program, dto.Active, dto.Version, ct);
            return result.Successful ? Ok(WorkflowResponse.From(result.Value)) : Failure(result.Error);
        }

        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var result = await _workflows.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        /// <summary>Runs it now (active or not). 202 with the run id, as api/v1/blocks/runs.</summary>
        [HttpPost("{id:guid}/runs")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Execute)]
        [ProducesResponseType<RunStartedResponse>(StatusCodes.Status202Accepted)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Run(Guid id, [FromBody] RunWorkflowRequest? dto, CancellationToken ct = default)
        {
            var result = await _workflows.RunAsync(User.GetUserId(), User.GetActorId(), id, dto?.Input, ct);
            if (!result.Successful)
                return Failure(result.Error);

            var runId = result.Value.RunId;
            return Accepted($"/api/v1/blocks/runs/{runId}", new RunStartedResponse(runId, BlockRunStatus.Queued));
        }

        /// <summary>Its runs, newest first, a page at a time (pass nextCursor as cursor).</summary>
        [HttpGet("{id:guid}/runs")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<BlockRunPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Runs(Guid id, [Range(1, 100)] int limit = 20, [MaxLength(512)] string? cursor = null,
            CancellationToken ct = default)
        {
            var workflow = await _workflows.GetAsync(User.GetUserId(), id, ct);
            if (!workflow.Successful)
                return Failure(workflow.Error);

            var page = await _runs.ListByWorkflowAsync(User.GetUserId(), id, limit, cursor, ct);
            return page.Successful
                ? Ok(new BlockRunPageResponse(page.Value.Items.Select(BlockRunResponse.From).ToList(), page.Value.NextCursor))
                : Failure(page.Error);
        }

        /// <summary>Saved versions, newest first: one per change of the program, the last 50.</summary>
        [HttpGet("{id:guid}/versions")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<WorkflowVersionResponse[]>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Versions(Guid id, CancellationToken ct = default)
        {
            var result = await _workflows.VersionsAsync(User.GetUserId(), id, ct);
            return result.Successful
                ? Ok(result.Value.Select(WorkflowVersionResponse.From))
                : Failure(result.Error);
        }

        [HttpGet("{id:guid}/versions/{version:int}")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<WorkflowVersionDetailsResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Version(Guid id, int version, CancellationToken ct = default)
        {
            var result = await _workflows.VersionAsync(User.GetUserId(), id, version, ct);
            return result.Successful ? Ok(WorkflowVersionDetailsResponse.From(result.Value)) : Failure(result.Error);
        }

        /// <summary>
        /// Makes that version current again as a new version (history is kept).
        /// Send the current version in the body to avoid overwriting a newer edit.
        /// </summary>
        [HttpPost("{id:guid}/versions/{version:int}/restore")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Update)]
        [ProducesResponseType<WorkflowResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Restore(Guid id, int version, [FromBody] RestoreWorkflowRequest? dto, CancellationToken ct = default)
        {
            var result = await _workflows.RestoreAsync(User.GetUserId(), User.GetActorId(), id, version, dto?.CurrentVersion, ct);
            return result.Successful ? Ok(WorkflowResponse.From(result.Value)) : Failure(result.Error);
        }
    }
}
