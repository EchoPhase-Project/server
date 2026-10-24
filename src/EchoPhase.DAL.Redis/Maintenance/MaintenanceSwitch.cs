// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.Logging;

namespace EchoPhase.DAL.Redis.Maintenance
{
    /// <summary>
    /// Maintenance mode for the whole deployment, switched at run time (CLI or
    /// API) rather than by configuration. Reads are cached per instance for
    /// <see cref="CacheFor"/>: every API request asks, Redis is asked at most
    /// that often. If Redis fails, the last known state stands; with none, it
    /// is off: Redis being down must not close the site by itself.
    /// </summary>
    public sealed class MaintenanceSwitch
    {
        public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(5);

        private sealed record Snapshot(MaintenanceState? State, long ReadAt);

        private readonly IMaintenanceStore _store;
        private readonly TimeProvider _time;
        private readonly ILogger<MaintenanceSwitch> _logger;
        private volatile Snapshot? _snapshot;

        public MaintenanceSwitch(IMaintenanceStore store, TimeProvider time, ILogger<MaintenanceSwitch> logger)
        {
            _store = store;
            _time = time;
            _logger = logger;
        }

        /// <summary>The state while maintenance is on; null when it is off.</summary>
        public async ValueTask<MaintenanceState?> CurrentAsync(CancellationToken ct = default)
        {
            var now = _time.GetTimestamp();
            var snapshot = _snapshot;
            if (snapshot is null || _time.GetElapsedTime(snapshot.ReadAt, now) >= CacheFor)
            {
                MaintenanceState? state;
                try
                {
                    state = await _store.GetAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not read the maintenance state; keeping the last known one.");
                    state = snapshot?.State;
                }
                _snapshot = snapshot = new Snapshot(state, now);
            }

            return snapshot.State is { } current && !current.IsOver(_time.GetUtcNow()) ? current : null;
        }

        /// <summary>Turns maintenance on for every instance (within <see cref="CacheFor"/>); with a duration, it ends by itself.</summary>
        public async Task<MaintenanceState> EnableAsync(string? message, TimeSpan? duration, CancellationToken ct = default)
        {
            if (duration is { } d && d <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(duration), "A maintenance lasts a positive time.");

            var now = _time.GetUtcNow();
            var state = new MaintenanceState(string.IsNullOrWhiteSpace(message) ? null : message.Trim(), now, now + duration);
            await _store.SetAsync(state, duration, ct);
            _snapshot = new Snapshot(state, _time.GetTimestamp());
            return state;
        }

        public async Task DisableAsync(CancellationToken ct = default)
        {
            await _store.ClearAsync(ct);
            _snapshot = new Snapshot(null, _time.GetTimestamp());
        }
    }
}
