// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Profilers
{
    public class ProfilerProvider : IProfilerProvider
    {
        private readonly object _lock = new();

        private readonly FlatProfiler _flatProfiler = new();
        private readonly StackProfiler _stackProfiler = new();

        public ProfilerProvider()
        {
            Current = _flatProfiler;
        }

        public IProfiler Current
        {
            get
            {
                lock (_lock)
                    return field;
            }

            private set;
        }

        public void Use(ProfilerTypes type)
        {
            lock (_lock)
            {
                Current = type switch
                {
                    ProfilerTypes.Flat => _flatProfiler,
                    ProfilerTypes.Stack => _stackProfiler,
                    _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
                };
            }
        }
    }
}
