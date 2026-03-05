namespace EchoPhase.Security.Antiforgery
{
    public interface IAntiforgeryService
    {
        void Set();
        void Remove();
        Task ValidateAsync();
    }
}
