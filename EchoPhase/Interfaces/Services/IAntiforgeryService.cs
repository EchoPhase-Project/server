namespace EchoPhase.Interfaces
{
    public interface IAntiforgeryService
    {
        public bool SetAntiforgeryToken();
        public string? GetAntiforgeryToken();
        public Task<bool> ValidateAntiforgeryTokenAsync();
    }
}
