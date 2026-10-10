// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Runs
{
    /// <summary>
    /// Queued and running runs of this instance: enforces the per-user limit
    /// and lets a run be cancelled before or while it executes.
    /// </summary>
    /// <remarks>
    /// In-process, like the queue: correct for a single instance. Several
    /// replicas would need the limit and cancellation in Redis.
    /// </remarks>
    public sealed class BlockRunRegistry
    {
        private readonly object _sync = new();
        private readonly Dictionary<Guid, Entry> _runs = new();
        private readonly Dictionary<Guid, int> _perUser = new();

        private sealed record Entry(Guid UserId, CancellationTokenSource Cancellation);

        /// <summary>
        /// Reserves a slot for a new run, or returns null when the user already
        /// has <paramref name="maxPerUser"/> active runs.
        /// </summary>
        public CancellationToken? TryReserve(Guid userId, Guid runId, int maxPerUser)
        {
            lock (_sync)
            {
                var active = _perUser.GetValueOrDefault(userId);
                if (active >= maxPerUser)
                    return null;

                var cancellation = new CancellationTokenSource();
                _runs[runId] = new Entry(userId, cancellation);
                _perUser[userId] = active + 1;
                return cancellation.Token;
            }
        }

        /// <summary>
        /// Cancels a run of <paramref name="userId"/>. False when it is not
        /// active here or belongs to someone else (indistinguishable on purpose).
        /// </summary>
        public bool TryCancel(Guid userId, Guid runId)
        {
            lock (_sync)
            {
                if (!_runs.TryGetValue(runId, out var entry) || entry.UserId != userId)
                    return false;

                entry.Cancellation.Cancel();
                return true;
            }
        }

        public bool IsActive(Guid runId)
        {
            lock (_sync)
                return _runs.ContainsKey(runId);
        }

        public void Release(Guid runId)
        {
            lock (_sync)
            {
                if (!_runs.Remove(runId, out var entry))
                    return;

                if (--_perUser[entry.UserId] == 0)
                    _perUser.Remove(entry.UserId);

                entry.Cancellation.Dispose();
            }
        }
    }
}
