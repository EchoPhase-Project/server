namespace EchoPhase.Interfaces
{
    public interface ITriggerRunner
    {
        Task<string> HandleAsync(string input);
    }
}
