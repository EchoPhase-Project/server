// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Server.Services.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Workflows
{
    /// <summary>
    /// Incoming webhooks: a POST here starts a run of an active workflow from
    /// its WebhookTrigger block, with the JSON body as the trigger's output.
    /// No session: the secret in X-Webhook-Secret is the credential.
    /// </summary>
    [Route("api/v1/hooks")]
    public class WebhooksController : ApiControllerBase
    {
        public const string SecretHeader = "X-Webhook-Secret";

        private readonly WorkflowService _workflows;

        public WebhooksController(WorkflowService workflows)
        {
            _workflows = workflows;
        }

        [HttpPost("{workflowId:guid}/{blockId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Trigger(
            Guid workflowId, int blockId, [FromHeader(Name = SecretHeader)] string? secret,
            [FromBody] JsonElement? body, CancellationToken ct = default)
        {
            var result = await _workflows.TriggerWebhookAsync(workflowId, blockId, secret, body, ct);
            if (!result.Successful)
                return Failure(result.Error);

            return Accepted(new { runId = result.Value!.RunId, status = BlockRunStatus.Queued });
        }
    }
}
