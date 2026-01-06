using EchoPhase.Enums;
using EchoPhase.Interfaces;

namespace EchoPhase.Analytics
{
    public class ProfilerProvider : IProfilerProvider
    {
        private readonly object _lock = new();

        private readonly FlatProfiler _flatProfiler = new();
        private readonly StackProfiler _stackProfiler = new();

        private IProfiler _current;

        public ProfilerProvider()
        {
            _current = _flatProfiler;
        }

        public IProfiler Current
        {
            get
            {
                lock (_lock)
                    return _current;
            }
        }

        public void Use(ProfilerTypes type)
        {
            lock (_lock)
            {
                _current = type switch
                {
                    ProfilerTypes.Flat => _flatProfiler,
                    ProfilerTypes.Stack => _stackProfiler,
                    _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
                };
            }
        }
    }
}
