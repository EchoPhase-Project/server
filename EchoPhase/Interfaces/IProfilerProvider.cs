using EchoPhase.Enums;

namespace EchoPhase.Interfaces
{
    public interface IProfilerProvider
    {
        IProfiler Current
        {
            get;
        }
        void Use(ProfilerTypes type);
    }
}
