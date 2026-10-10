// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;

namespace EchoPhase.Profilers.Models
{
    /// <summary>Timings of one step; safe to update from several threads.</summary>
    public sealed class ProfileNode
    {
        private readonly object _sync = new();
        private readonly List<ProfileNode> _children = [];
        private long _totalTicks;
        private long _minTicks = long.MaxValue;
        private long _maxTicks;
        private int _count;
        private long _allocated;

        public ProfileNode(string name, ProfileNode? parent)
        {
            Name = name;
            Parent = parent;
        }

        public string Name { get; }

        public ProfileNode? Parent { get; }

        public int Count { get { lock (_sync) return _count; } }

        public double AverageMs { get { lock (_sync) return _count > 0 ? ToMs(_totalTicks) / _count : 0; } }

        public double MinMs { get { lock (_sync) return _count > 0 ? ToMs(_minTicks) : 0; } }

        public double MaxMs { get { lock (_sync) return ToMs(_maxTicks); } }

        public double TotalMs { get { lock (_sync) return ToMs(_totalTicks); } }

        /// <summary>Bytes allocated by the step on the thread it ran on.</summary>
        public long AllocatedBytes { get { lock (_sync) return _allocated; } }

        public void AddMeasurement(long ticks, long allocated)
        {
            lock (_sync)
            {
                _count++;
                _totalTicks += ticks;
                _minTicks = Math.Min(_minTicks, ticks);
                _maxTicks = Math.Max(_maxTicks, ticks);
                _allocated += allocated;
            }
        }

        public ProfileNode GetOrAddChild(string name)
        {
            lock (_sync)
            {
                foreach (var child in _children)
                    if (child.Name == name)
                        return child;

                var created = new ProfileNode(name, this);
                _children.Add(created);
                return created;
            }
        }

        public List<ProfileNode> SnapshotChildren()
        {
            lock (_sync)
                return [.. _children];
        }

        public void ClearChildren()
        {
            lock (_sync)
                _children.Clear();
        }

        private static double ToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }
}
