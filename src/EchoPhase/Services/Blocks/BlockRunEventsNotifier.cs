// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Interfaces.Services;
using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Services.Blocks
{
    /// <summary>
    /// Tells the owner that a run started or finished. Events carry the status
    /// only: the result is fetched from the API, which also covers clients that
    /// were offline when the event was sent.
    /// </summary>
    public class BlockRunEventsNotifier
    {
        private readonly IEventService _events;

        public BlockRunEventsNotifier(IEventService events)
        {
            _events = events;
        }

        public Task StartedAsync(BlockRun run, CancellationToken ct = default)
            => SendAsync(run, "block_run.started", new { runId = run.RunId, status = run.Status }, ct);

        public Task CompletedAsync(BlockRun run, CancellationToken ct = default)
            => SendAsync(run, "block_run.completed", new
            {
                runId = run.RunId,
                status = run.Status,
                durationMs = run.DurationMs,
                errorCount = run.ErrorCount
            }, ct);

        /// <summary>Where a running run is: steps so far and the block just finished.</summary>
        public Task ProgressAsync(Guid userId, Guid runId, int steps, int blockId, string blockType, CancellationToken ct = default)
            => SendAsync(userId, "block_run.progress", new { runId, steps, blockId, blockType }, ct);

        /// <summary>A block failed; the run may stop (handler error) or go on (skipped block).</summary>
        public Task BlockFailedAsync(Guid userId, Guid runId, int blockId, string errorType, string message, CancellationToken ct = default)
            => SendAsync(userId, "block_run.block_failed", new { runId, blockId, errorType, message }, ct);

        private Task SendAsync(BlockRun run, string type, object data, CancellationToken ct)
            => SendAsync(run.UserId, type, data, ct);

        private Task SendAsync(Guid userId, string type, object data, CancellationToken ct)
            => _events.SendMessageToUsersAsync(
                new HashSet<Guid> { userId },
                new { type, run = data },
                new HashSet<string> { Intents.Blocks },
                ct);
    }
}
