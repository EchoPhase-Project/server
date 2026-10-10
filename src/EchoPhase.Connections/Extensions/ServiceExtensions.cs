// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections.Background;
using EchoPhase.Connections.Http;
using EchoPhase.Connections.Locking;
using EchoPhase.Connections.Providers;
using EchoPhase.Connections.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EchoPhase.Connections.Extensions
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// Registers connection storage, OAuth flow and background validation.
        /// The provider HttpClients (e.g. <c>ITwitchAuthClient</c>) are registered by the host.
        /// </summary>
        public static IServiceCollection AddExternalConnections(this IServiceCollection services)
        {
            services.AddKeyedScoped<IConnectionProvider, DiscordConnectionProvider>(ConnectionProviders.Discord);
            services.AddKeyedScoped<IConnectionProvider, TwitchConnectionProvider>(ConnectionProviders.Twitch);
            services.AddScoped<IConnectionProviderRegistry, ConnectionProviderRegistry>();

            services.AddSingleton<SecretProtector>();
            services.AddSingleton<IConnectionLock, RedisConnectionLock>();
            services.TryAddScoped<IConnectionEvents, NullConnectionEvents>();

            services.AddScoped<IOAuthAppService, OAuthAppService>();
            services.AddScoped<IConnectionService, ConnectionService>();
            services.AddScoped<IConnectionOAuthService, ConnectionOAuthService>();

            services.AddHostedService<ConnectionValidationWorker>();

            return services;
        }

        /// <summary>
        /// Authenticates this client's requests with stored connections of
        /// <paramref name="provider"/>. Call before adding retry policies: the
        /// handler then sits outside them, so its own 401 retry happens once and
        /// its synthesised errors (e.g. 404 for a missing connection) are not retried.
        /// </summary>
        public static IHttpClientBuilder AddConnectionAuth(this IHttpClientBuilder builder, string provider)
            => builder.AddHttpMessageHandler(sp =>
                new ConnectionAuthHandler(provider, sp.GetRequiredService<IServiceScopeFactory>()));
    }
}
