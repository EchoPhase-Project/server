// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Server.Services.Blocks
{
    /// <summary>What started a run (BlockRun.Trigger).</summary>
    public static class BlockRunTriggers
    {
        public const string Manual = "manual";
        public const string Webhook = "webhook";
        public const string Schedule = "schedule";
        public const string Discord = "discord";

        /// <summary>An ErrorTrigger: another run failed. Such runs never start error workflows themselves.</summary>
        public const string Error = "error";

        /// <summary>A failed run started again by its owner.</summary>
        public const string Retry = "retry";
    }

    /// <summary>
    /// What to run: the program, where to start (null: its manual start),
    /// the first blocks' input, where it came from, and the webhook caller
    /// waiting for its answer, if any.
    /// </summary>
    public sealed record BlockRunStart(
        EchoPhase.Runners.Blocks.Models.BlockProgram Program,
        IReadOnlyCollection<int>? StartIds = null,
        object? Input = null,
        Guid? WorkflowId = null,
        string Trigger = BlockRunTriggers.Manual,
        EchoPhase.Runners.Blocks.Contexts.WebhookReply? Reply = null);
}
