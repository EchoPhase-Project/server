// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EchoPhase.DAL.Postgres.Configurations;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Runners.Blocks.Storage;
using EchoPhase.Scripting;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Server.Services.Workflows;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Storage
{
    /// <param name="Key">The key.</param>
    /// <param name="Size">Bytes of the value as JSON.</param>
    /// <param name="UpdatedAt">Last write.</param>
    /// <param name="ExpiresAt">When it goes; null: kept until deleted.</param>
    /// <param name="Value">The value; only when one entry is fetched.</param>
    public sealed record StorageEntryResponse(string Key, int Size, DateTime UpdatedAt, DateTime? ExpiresAt, JsonElement? Value);

    /// <param name="Value">Any JSON value.</param>
    /// <param name="TtlSeconds">Seconds until it expires; absent: kept until deleted.</param>
    public sealed record PutStorageRequest(JsonElement Value, [Range(1, int.MaxValue)] int? TtlSeconds = null);

    /// <summary>
    /// What workflows keep between runs (the Storage block): a workflow's own
    /// values with ?workflowId=, the ones a package's workflows share with
    /// ?packageId= (one of the two). To look at, seed or clear them; runs read
    /// and write them themselves.
    /// </summary>
    [Route("api/v1/storage")]
    public class StorageController : ApiControllerBase
    {
        private const string StorageLimit = "StorageLimit";

        private readonly IWorkflowStore _store;
        private readonly WorkflowService _workflows;
        private readonly WorkflowPackageService _packages;

        public StorageController(IWorkflowStore store, WorkflowService workflows, WorkflowPackageService packages)
        {
            _store = store;
            _workflows = workflows;
            _packages = packages;
        }

        /// <summary>Keys in order (no values), a page at a time: pass the last key as `after`.</summary>
        [HttpGet]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<StorageEntryResponse[]>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> List(
            Guid? workflowId, Guid? packageId, string? prefix, string? after,
            [Range(1, StorageParams.MaxListLimit)] int limit = 100, CancellationToken ct = default)
        {
            if (await SlotAsync(workflowId, packageId, ct) is not { } slot)
                return WorkflowNotFound();
            var items = await _store.ListAsync(slot, prefix, after, limit, values: false, ct);
            return Ok(items.Select(i => View(i, withValue: false)));
        }

        [HttpGet("value")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<StorageEntryResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get([Required] string key, Guid? workflowId, Guid? packageId, CancellationToken ct = default)
        {
            if (await SlotAsync(workflowId, packageId, ct) is not { } slot)
                return WorkflowNotFound();
            var item = await _store.GetAsync(slot, key, ct);
            return item is null
                ? Error(ErrorCodes.NotFound, $"No value under '{key}'.", StatusCodes.Status404NotFound)
                : Ok(View(item, withValue: true));
        }

        /// <summary>Writes a value, as a Storage block's set would.</summary>
        [HttpPut("value")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Update)]
        [ProducesResponseType<StorageEntryResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Put(
            [Required][MaxLength(StorageEntryConfiguration.MaxKeyLength)] string key, Guid? workflowId, Guid? packageId,
            [FromBody] PutStorageRequest dto, CancellationToken ct = default)
        {
            if (await SlotAsync(workflowId, packageId, ct) is not { } slot)
                return WorkflowNotFound();
            try
            {
                var ttl = dto.TtlSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : (TimeSpan?)null;
                await _store.SetAsync(slot, key, Eval.Plain(dto.Value), ttl, ct);
            }
            catch (StorageLimitException ex)
            {
                return Error(StorageLimit, ex.Message, StatusCodes.Status400BadRequest);
            }
            return await _store.GetAsync(slot, key, ct) is { } item
                ? Ok(View(item, withValue: true))
                : Error(ErrorCodes.NotFound, "The value expired or was deleted meanwhile.", StatusCodes.Status404NotFound);
        }

        [HttpDelete("value")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Update)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete([Required] string key, Guid? workflowId, Guid? packageId, CancellationToken ct = default)
        {
            if (await SlotAsync(workflowId, packageId, ct) is not { } slot)
                return WorkflowNotFound();
            return await _store.DeleteAsync(slot, key, ct)
                ? NoContent()
                : Error(ErrorCodes.NotFound, $"No value under '{key}'.", StatusCodes.Status404NotFound);
        }

        // A workflow's storage, or its package's shared one, only for their owner.
        private async Task<StorageSlot?> SlotAsync(Guid? workflowId, Guid? packageId, CancellationToken ct)
        {
            var userId = User.GetUserId();
            if (workflowId is { } id)
            {
                var workflow = await _workflows.GetAsync(userId, id, ct);
                return workflow.Successful ? new StorageSlot(userId, workflow.Value.Workflow.PackageId, id) : null;
            }
            if (packageId is { } package)
            {
                var found = await _packages.GetAsync(userId, package, ct);
                return found.Successful && found.Value.Access == PackageAccess.Owner ? new StorageSlot(userId, package, null) : null;
            }
            return null;
        }

        private ObjectResult WorkflowNotFound()
            => Error(ErrorCodes.NotFound, "No such workflow or package of yours (give workflowId or packageId).",
                StatusCodes.Status404NotFound);

        private static StorageEntryResponse View(StorageItem item, bool withValue)
            => new(item.Key, item.Size, item.UpdatedAt, item.ExpiresAt,
                withValue ? JsonSerializer.SerializeToElement(item.Value) : null);
    }
}
