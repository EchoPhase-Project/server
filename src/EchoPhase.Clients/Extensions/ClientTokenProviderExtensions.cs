// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Clients.Extensions
{
    public static class ClientTokenProviderExtensions
    {
        public static IServiceCollection AddClientTokenProviders(
            this IServiceCollection services)
        {
            services.AddKeyedScoped<IClientTokenProvider, DiscordTokenProvider>(DiscordTokenProvider.ProviderName);

            services.AddScoped<IClientTokenProviderRegistry, ClientTokenProviderRegistry>();

            return services;
        }
    }
}
