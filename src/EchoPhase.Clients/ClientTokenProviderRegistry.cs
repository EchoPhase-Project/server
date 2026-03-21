using EchoPhase.Clients.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Clients
{
    public class ClientTokenProviderRegistry : IClientTokenProviderRegistry
    {
        private readonly IServiceProvider _services;

        public ClientTokenProviderRegistry(IServiceProvider services)
        {
            _services = services;
        }

        public IClientTokenProvider Get(string providerName)
            => _services.GetKeyedService<IClientTokenProvider>(providerName)
               ?? throw new KeyNotFoundException($"No token provider for '{providerName}'.");
    }
}
