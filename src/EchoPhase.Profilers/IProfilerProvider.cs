namespace EchoPhase.Profilers
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
