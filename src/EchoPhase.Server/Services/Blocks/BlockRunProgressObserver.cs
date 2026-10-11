// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Observers;

namespace EchoPhase.Server.Services.Blocks
{
    /// <summary>
    /// Live progress of a stored run for its owner (Blocks intent): failures
    /// at once, progress at most every <see cref="Interval"/>, since a run
    /// can take thousands of steps. One per run (scoped to the run's scope).
    /// </summary>
    public sealed class BlockRunProgressObserver : IBlockRunObserver
    {
        public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

        private readonly BlockRunEventsNotifier _events;
        private readonly TimeProvider _time;
        private long _lastSent;

        public BlockRunProgressObserver(BlockRunEventsNotifier events, TimeProvider time)
        {
            _events = events;
            _time = time;
        }

        public async ValueTask OnBlockCompletedAsync(BlockCompletedEvent e)
        {
            // Runs outside a stored one (none yet) have nobody to report to.
            if (e.Context.RunId == Guid.Empty)
                return;

            long now = _time.GetTimestamp();
            if (_lastSent != 0 && _time.GetElapsedTime(_lastSent, now) < Interval)
                return;
            _lastSent = now;

            await _events.ProgressAsync(e.Context.UserId, e.Context.RunId, e.Context.Steps, e.Block.Id,
                e.Block.Type.ToString(), e.Context.CancellationToken);
        }

        public async ValueTask OnBlockFailedAsync(BlockFailedEvent e)
        {
            // Retries and handled errors are not failures of the run.
            if (e.Context.RunId == Guid.Empty || e.WillRetry || e.Handled)
                return;

            await _events.BlockFailedAsync(e.Context.UserId, e.Context.RunId, e.Block.Id,
                e.Error.Type, e.Error.Message, e.Context.CancellationToken);
        }
    }
}
