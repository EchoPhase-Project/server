// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;

namespace EchoPhase.Profilers.Models
{
    internal class StepTimer : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly ProfileNode _node;
        private readonly long _memoryBefore;
        private readonly Action? _onDispose;

        public StepTimer(ProfileNode node, Action? onDispose = null)
        {
            _node = node;
            _onDispose = onDispose;
            _memoryBefore = GC.GetTotalMemory(false);
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            long memoryAfter = GC.GetTotalMemory(false);
            long memChange = memoryAfter - _memoryBefore;

            lock (_node)
            {
                _node.AddMeasurement(_stopwatch.ElapsedTicks, memChange);
            }
            _onDispose?.Invoke();
        }
    }
}
