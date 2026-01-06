using System.Diagnostics;

namespace EchoPhase.Analytics.Models
{
    internal class ProfileNode
    {
        public string Name
        {
            get;
        }
        public List<ProfileNode> Children
        {
            get;
        }

        private long _totalTicks;
        private int _count;
        private long _minTicks = long.MaxValue;
        private long _maxTicks = long.MinValue;
        private long _totalMemoryChangeBytes;

        public ProfileNode(string name)
        {
            Name = name;
            Children = new List<ProfileNode>();
        }

        public void AddMeasurement(long ticks, long memoryChange)
        {
            _count++;
            _totalTicks += ticks;
            _minTicks = Math.Min(_minTicks, ticks);
            _maxTicks = Math.Max(_maxTicks, ticks);
            _totalMemoryChangeBytes += memoryChange;
        }

        public int Count => _count;
        public double AverageMs => _count > 0 ? (_totalTicks / (double)_count) * 1000.0 / Stopwatch.Frequency : 0;
        public double MinMs => _minTicks == long.MaxValue ? 0 : _minTicks * 1000.0 / Stopwatch.Frequency;
        public double MaxMs => _maxTicks == long.MinValue ? 0 : _maxTicks * 1000.0 / Stopwatch.Frequency;
        public double TotalMs => _totalTicks * 1000.0 / Stopwatch.Frequency;
        public long TotalMemoryChangeBytes => _totalMemoryChangeBytes;
    }
}
