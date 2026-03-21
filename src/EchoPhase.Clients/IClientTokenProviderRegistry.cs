using EchoPhase.Clients.Providers;

namespace EchoPhase.Clients
{
    public interface IClientTokenProviderRegistry
    {
        IClientTokenProvider Get(string providerName);
    }
}
