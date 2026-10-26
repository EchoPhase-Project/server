// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Server.Services.Blocks;
using EchoPhase.Types.Result.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Blocks
{
    /// <summary>
    /// Block programs run in the background: starting one returns its id at
    /// once, progress arrives as <c>block_run.*</c> events (intent <c>blocks</c>)
    /// and the result is fetched here.
    /// </summary>
    [Route("api/v1/blocks/runs")]
    public class BlockRunsController : ApiControllerBase
    {
        private readonly BlockRunService _runs;

        public BlockRunsController(BlockRunService runs)
        {
            _runs = runs;
        }

        /// <summary>
        /// Validates and queues a program. 202 with the run id; 400 for an
        /// invalid program, 429 when the caller has too many active runs,
        /// 503 when the queue is full.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Scripts, Permissions.Execute)]
        [ProducesResponseType<RunStartedResponse>(StatusCodes.Status202Accepted)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Start([FromBody] RunBlocksRequest request, CancellationToken ct = default)
        {
            var start = new BlockRunStart(request, request.StartIds.Count > 0 ? request.StartIds : null, request.Input);
            var result = await _runs.StartAsync(User.GetUserId(), User.GetActorId(), start, ct);

            if (!result.Successful)
                return Failure(result.Error);

            // The worker may already be updating the run; at this point it is queued.
            var runId = result.Value.RunId;
            return AcceptedAtAction(nameof(Get), new { runId }, new RunStartedResponse(runId, BlockRunStatus.Queued));
        }

        /// <summary>The caller's runs, newest first.</summary>
        [HttpGet]
        [RequirePermission(Resources.Scripts, Permissions.Read)]
        [ProducesResponseType<BlockRunPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> List(
            [FromQuery] int limit = 20, [FromQuery] string? cursor = null, CancellationToken ct = default)
        {
            var result = await _runs.ListAsync(User.GetUserId(), limit, cursor, ct);
            if (!result.Successful)
                return Failure(result.Error);

            var page = result.Value;
            return Ok(new BlockRunPageResponse(page.Items.Select(BlockRunResponse.From).ToList(), page.NextCursor));
        }

        /// <summary>
        /// A run and, until it expires, its result (output, variables, errors).
        /// Another user's run is reported as not found.
        /// </summary>
        [HttpGet("{runId:guid}")]
        [RequirePermission(Resources.Scripts, Permissions.Read)]
        [ProducesResponseType<BlockRunDetailsResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid runId, CancellationToken ct = default)
        {
            var result = await _runs.GetAsync(User.GetUserId(), runId, ct);
            if (!result.Successful)
                return Failure(result.Error);

            var details = result.Value;
            return Ok(new BlockRunDetailsResponse(BlockRunResponse.From(details.Run), details.Result));
        }

        /// <summary>
        /// Cancels a queued or running run. The run stays (as Cancelled), so
        /// this is an action on it, not a DELETE. 202: the run reports its end
        /// as an event.
        /// </summary>
        [HttpPost("{runId:guid}/cancel")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Scripts, Permissions.Execute)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(Guid runId, CancellationToken ct = default)
        {
            var result = await _runs.CancelAsync(User.GetUserId(), runId, ct);
            return result.Successful ? Accepted() : Failure(result.Error);
        }

        /// <summary>
        /// Starts a run that did not succeed again with the same input. With
        /// <c>workflow=current</c>, a workflow's run uses the workflow as saved
        /// now; by default the program as it ran. 202 with the new run's id.
        /// </summary>
        [HttpPost("{runId:guid}/retry")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Scripts, Permissions.Execute)]
        [ProducesResponseType<RunStartedResponse>(StatusCodes.Status202Accepted)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Retry(Guid runId, [FromQuery] string workflow = "original", CancellationToken ct = default)
        {
            if (workflow is not ("original" or "current"))
                return Error(BlockRunErrors.Invalid, "workflow must be 'original' or 'current'.", StatusCodes.Status400BadRequest);

            var result = await _runs.RetryAsync(User.GetUserId(), User.GetActorId(), runId, workflow == "current", ct);
            if (!result.TryGetValue(out var retry))
                return result.Successful
                    ? throw new InvalidOperationException($"Retrying run {runId} succeeded without a new run.")
                    : Failure(result.Error);

            var newRunId = retry.RunId;
            return AcceptedAtAction(nameof(Get), new { runId = newRunId }, new RunStartedResponse(newRunId, BlockRunStatus.Queued, runId));
        }
    }
}
