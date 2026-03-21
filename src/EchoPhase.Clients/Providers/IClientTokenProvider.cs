namespace EchoPhase.Clients.Providers
{
    public interface IClientTokenProvider
    {
        Task<byte[]> ResolveAsync(Guid userId, string tokenName);
    }
}
