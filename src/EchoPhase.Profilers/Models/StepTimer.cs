// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;

namespace EchoPhase.Profilers.Models
{
    /// <summary>
    /// Times one step. Allocations are counted on the current thread
    /// (GC.GetAllocatedBytesForCurrentThread): exact for a synchronous step,
    /// unlike the whole heap's size, which other threads change too. A step
    /// that awaits may finish on another thread; its count is then left out.
    /// </summary>
    internal sealed class StepTimer : IDisposable
    {
        private readonly ProfileNode _node;
        private readonly Action? _onDispose;
        private readonly long _start;
        private readonly long _allocatedBefore;
        private readonly int _threadId;
        private bool _disposed;

        public StepTimer(ProfileNode node, Action? onDispose = null)
        {
            _node = node;
            _onDispose = onDispose;
            _threadId = Environment.CurrentManagedThreadId;
            _allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            long ticks = Stopwatch.GetTimestamp() - _start;
            long allocated = Environment.CurrentManagedThreadId == _threadId
                ? GC.GetAllocatedBytesForCurrentThread() - _allocatedBefore
                : 0;
            _node.AddMeasurement(ticks, allocated);
            _onDispose?.Invoke();
        }
    }
}
