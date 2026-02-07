namespace EchoPhase.Profilers.Models
{
    internal class DisposableEmpty : IDisposable
    {
        public static readonly DisposableEmpty Instance = new();
        private DisposableEmpty()
        {
        }
        public void Dispose()
        {
        }
    }
}
