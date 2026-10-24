// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Configuration.Runner;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Scripting;
using EchoPhase.Server.Services.Workflows;
using EchoPhase.Types.Result.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Controllers.Api.v1.Workflows
{
    /// <summary>
    /// Incoming webhooks: a POST here starts a run of an active workflow from
    /// its WebhookTrigger block, with the JSON body as the trigger's output.
    /// No session: the secret in X-Webhook-Secret is the credential. The
    /// trigger's respond mode decides the answer: 202 at once, or what the
    /// run (its last block, or a RespondToWebhook) gives.
    /// </summary>
    [Route("api/v1/hooks")]
    public class WebhooksController : ApiControllerBase
    {
        public const string SecretHeader = "X-Webhook-Secret";

        /// <summary>Time a waiting caller allows the run to get a worker, on top of its own time limit.</summary>
        private static readonly TimeSpan QueueAllowance = TimeSpan.FromSeconds(30);

        private readonly WorkflowService _workflows;
        private readonly RunnerOptions _options;

        public WebhooksController(WorkflowService workflows, IOptions<RunnerOptions> options)
        {
            _workflows = workflows;
            _options = options.Value;
        }

        [HttpPost("{workflowId:guid}/{blockId:int}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
        public async Task<IActionResult> Trigger(
            Guid workflowId, int blockId, [FromHeader(Name = SecretHeader)] string? secret,
            [FromBody] JsonElement? body, CancellationToken ct = default)
        {
            var result = await _workflows.TriggerWebhookAsync(workflowId, blockId, secret, body, ct);
            if (!result.TryGetValue(out var started))
                return result.Successful
                    ? throw new InvalidOperationException($"Webhook of workflow {workflowId} started without a run.")
                    : Failure(result.Error);

            var (run, reply) = started;
            if (reply is null)
                return Accepted(new { runId = run.RunId, status = BlockRunStatus.Queued });

            WebhookResponse response;
            try
            {
                response = await reply.Response.WaitAsync(_options.Blocks.Timeout + QueueAllowance, ct);
            }
            catch (TimeoutException)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { runId = run.RunId, status = BlockRunStatus.Running });
            }

            return Respond(response);
        }

        // The program's answer, served from this origin: only its allowed
        // headers, and nothing in it may run as a page of the app.
        private IActionResult Respond(WebhookResponse response)
        {
            string? contentType = null;
            foreach (var (name, value) in response.Headers)
            {
                if (!WebhookResponse.IsAllowedHeader(name))
                    continue;
                if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    contentType = value;
                else
                    Response.Headers[name] = value;
            }
            Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
            Response.Headers.XContentTypeOptions = "nosniff";

            return Eval.Plain(response.Body) switch
            {
                null => StatusCode(response.Status),
                string text => new ContentResult
                {
                    StatusCode = response.Status,
                    Content = text,
                    ContentType = contentType ?? "text/plain; charset=utf-8"
                },
                var value => new ContentResult
                {
                    StatusCode = response.Status,
                    Content = Eval.ToJson(value),
                    ContentType = contentType ?? "application/json; charset=utf-8"
                }
            };
        }
    }
}
