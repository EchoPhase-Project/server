using System.Diagnostics;

namespace EchoPhase.Analytics.Models
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
