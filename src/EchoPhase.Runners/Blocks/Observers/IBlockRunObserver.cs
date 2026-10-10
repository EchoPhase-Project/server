// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Observers
{
    /// <summary>
    /// Watches a run block by block: progress for the webapp, tracing, metrics.
    /// Registered in DI (scoped: one per run's scope); every method has a
    /// no-op default, so an observer implements what it needs. An observer
    /// that throws is logged and skipped; the run goes on.
    /// </summary>
    /// <remarks>
    /// Called on the run's path, for every step (loop bodies included, up to
    /// MaxSteps): keep it cheap, batch or throttle anything slow.
    /// </remarks>
    public interface IBlockRunObserver
    {
        ValueTask OnBlockStartingAsync(BlockEvent e) => ValueTask.CompletedTask;

        ValueTask OnBlockCompletedAsync(BlockCompletedEvent e) => ValueTask.CompletedTask;

        ValueTask OnBlockFailedAsync(BlockFailedEvent e) => ValueTask.CompletedTask;
    }
}
