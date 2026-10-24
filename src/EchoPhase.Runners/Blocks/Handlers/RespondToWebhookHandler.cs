// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockType(BlockTypes.RespondToWebhook,
        Description = "Answers the webhook caller (when its trigger responds byBlock or onFinish); the first answer wins. Its input passes through.")]
    public class RespondToWebhookHandler : BlockHandlerBase<RespondToWebhookParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, RespondToWebhookParams param)
        {
            if (call.Context.Reply is { HasResponded: false } reply)
            {
                var scope = call.Scope;
                var body = param.Body.ValueKind == JsonValueKind.Undefined ? call.Input : BlockValues.Evaluate(param.Body, scope);
                var headers = param.Headers.ToDictionary(h => h.Key, h => Eval.TypedProcess<string>(h.Value, scope));
                reply.TrySend(new WebhookResponse(param.Status, body, headers));
            }

            return Task.FromResult(BlockResult.Main(call.Input));
        }
    }
}
