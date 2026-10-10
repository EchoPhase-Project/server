// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Params;

namespace EchoPhase.Runners.Blocks.Handlers
{
    // A trigger only hands the event to the program: its output is the
    // run's input (the request body, the schedule time, the caller's data).

    [BlockType(BlockTypes.ManualTrigger, IsTrigger = true, Description = "Starts a run from the API or the editor.")]
    public class ManualTriggerHandler : BlockHandlerBase<ManualTriggerParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, ManualTriggerParams param)
            => Task.FromResult(BlockResult.Main(call.Input));
    }

    [BlockType(BlockTypes.WebhookTrigger, IsTrigger = true, Description = "Starts a run on POST to the workflow's webhook URL; output: the request body.")]
    public class WebhookTriggerHandler : BlockHandlerBase<WebhookTriggerParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, WebhookTriggerParams param)
            => Task.FromResult(BlockResult.Main(call.Input));
    }

    [BlockType(BlockTypes.ScheduleTrigger, IsTrigger = true, Description = "Starts a run on a cron schedule in the workflow's time zone.")]
    public class ScheduleTriggerHandler : BlockHandlerBase<ScheduleTriggerParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, ScheduleTriggerParams param)
            => Task.FromResult(BlockResult.Main(call.Input));
    }

    [BlockType(BlockTypes.DiscordEventTrigger, IsTrigger = true, Description = "Starts a run on a Discord gateway event; output: { event, connection, data }.")]
    public class DiscordEventTriggerHandler : BlockHandlerBase<DiscordEventTriggerParams>
    {
        public override Task<BlockResult> ExecuteAsync(BlockCall call, DiscordEventTriggerParams param)
            => Task.FromResult(BlockResult.Main(call.Input));
    }
}
