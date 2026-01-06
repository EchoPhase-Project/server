namespace EchoPhase.Analytics.Models
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
