// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Connections.Providers
{
    public class ConnectionProviderRegistry : IConnectionProviderRegistry
    {
        private readonly IServiceProvider _services;

        public ConnectionProviderRegistry(IServiceProvider services)
        {
            _services = services;
        }

        public IConnectionProvider? Find(string providerName)
            => _services.GetKeyedService<IConnectionProvider>(providerName.ToLowerInvariant());
    }
}
