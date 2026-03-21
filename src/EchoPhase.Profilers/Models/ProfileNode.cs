// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;

namespace EchoPhase.Profilers.Models
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
        private long _minTicks = long.MaxValue;
        private long _maxTicks = long.MinValue;

        public ProfileNode(string name)
        {
            Name = name;
            Children = new List<ProfileNode>();
        }

        public void AddMeasurement(long ticks, long memoryChange)
        {
            Count++;
            _totalTicks += ticks;
            _minTicks = Math.Min(_minTicks, ticks);
            _maxTicks = Math.Max(_maxTicks, ticks);
            TotalMemoryChangeBytes += memoryChange;
        }

        public int Count { get; private set; }
        public double AverageMs => Count > 0 ? (_totalTicks / (double)Count) * 1000.0 / Stopwatch.Frequency : 0;
        public double MinMs => _minTicks == long.MaxValue ? 0 : _minTicks * 1000.0 / Stopwatch.Frequency;
        public double MaxMs => _maxTicks == long.MinValue ? 0 : _maxTicks * 1000.0 / Stopwatch.Frequency;
        public double TotalMs => _totalTicks * 1000.0 / Stopwatch.Frequency;
        public long TotalMemoryChangeBytes { get; private set; }
    }
}
