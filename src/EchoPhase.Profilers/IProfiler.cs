using System.Runtime.CompilerServices;

namespace EchoPhase.Profilers
{
    public interface IProfiler
    {
        bool IsEnabled
        {
            get;
        }

        void Enable();
        void Disable();
        void Clear();

        IDisposable Step([CallerMemberName] string memberName = "");
        void Step(Action action, [CallerMemberName] string memberName = "");
        T Step<T>(Func<T> action, [CallerMemberName] string memberName = "");
        Task StepAsync(Func<Task> func, [CallerMemberName] string memberName = "");
        Task<T> StepAsync<T>(Func<Task<T>> func, [CallerMemberName] string memberName = "");

        string Dump();
    }
}
