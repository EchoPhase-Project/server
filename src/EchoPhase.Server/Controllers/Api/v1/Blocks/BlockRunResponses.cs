// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.DAL.Scylla.Entities;

namespace EchoPhase.Server.Controllers.Api.v1.Blocks
{
    /// <param name="RunId">The run's id.</param>
    /// <param name="Status">queued, running, succeeded, failed, cancelled, timedOut or interrupted.</param>
    /// <param name="ActorId">Who started it when it was someone acting for the owner.</param>
    /// <param name="WorkflowId">The workflow it ran, if any.</param>
    /// <param name="ProgramHash">SHA-256 of the program it ran.</param>
    /// <param name="Trigger">What started it: manual, webhook, schedule, event, error, retry.</param>
    /// <param name="BlockCount">Blocks in its program.</param>
    /// <param name="CreatedAt">When it was queued.</param>
    /// <param name="StartedAt">When it started running.</param>
    /// <param name="FinishedAt">When it ended.</param>
    /// <param name="DurationMs">How long it ran.</param>
    /// <param name="Steps">Blocks it executed (a loop counts each time).</param>
    /// <param name="ErrorCount">Block errors it had.</param>
    public sealed record BlockRunResponse(Guid RunId, string Status, Guid? ActorId, Guid? WorkflowId, string? ProgramHash,
        string Trigger, int BlockCount, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
        long? DurationMs, int? Steps, int? ErrorCount)
    {
        public static BlockRunResponse From(BlockRun run) => new(run.RunId, run.Status, run.ActorId, run.WorkflowId,
            run.ProgramHash, run.Trigger, run.BlockCount, run.CreatedAt, run.StartedAt, run.FinishedAt, run.DurationMs,
            run.Steps, run.ErrorCount);
    }

    /// <param name="Data">The runs, newest first.</param>
    /// <param name="NextCursor">Where the next page starts; null: this is the last.</param>
    public sealed record BlockRunPageResponse(IReadOnlyList<BlockRunResponse> Data, string? NextCursor);

    /// <param name="Run">The run.</param>
    /// <param name="Result">Until it expires: its output, variables and errors, as the runner wrote them.</param>
    public sealed record BlockRunDetailsResponse(BlockRunResponse Run, JsonElement? Result);

    /// <param name="RunId">The new run's id.</param>
    /// <param name="Status">queued: it reports its progress as events.</param>
    /// <param name="RetryOf">The run it repeats, for a retry.</param>
    public sealed record RunStartedResponse(Guid RunId, string Status, Guid? RetryOf = null);
}
