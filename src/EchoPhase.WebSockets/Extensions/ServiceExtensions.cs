// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Cluster;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors;
using EchoPhase.WebSockets.Processors.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EchoPhase.WebSockets.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddWebSocket(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<WebSocketConnectionManager>();
            // One replica by default; AddRedisGatewayBackplane shares it with the others.
            services.TryAddSingleton<IGatewayBackplane, LocalGatewayBackplane>();
            services.AddSingleton<GatewayDispatcher>();
            services.AddHostedService<GatewayClusterService>();
            services.AddHostedService<GatewayHeartbeatMonitor>();
            services.AddScoped<WebSocketService>();
            services.AddScoped<WebSocketProcessor>();
            services.AddScoped<OpCodeHandlerResolver>();
            services.AddScoped<IntentAuthorization>();

            // Scoped: resolved from the connection's request scope.
            services.AddKeyedScoped<IOpCodeHandler, HandshakeHandler>(OpCodes.Handshake);
            services.AddKeyedScoped<IOpCodeHandler, DisconnectHandler>(OpCodes.Disconnect);
            services.AddKeyedScoped<IOpCodeHandler, PingHandler>(OpCodes.Ping);
            services.AddKeyedScoped<IOpCodeHandler, AdjustHandler>(OpCodes.Adjust);

            return services;
        }

        /// <summary>
        /// Replicas share deliveries and shard pools through Redis (the
        /// application's multiplexer, resolved when first used).
        /// </summary>
        public static IServiceCollection AddRedisGatewayBackplane(this IServiceCollection services)
        {
            services.RemoveAll<IGatewayBackplane>();
            services.AddSingleton<IGatewayBackplane>(sp => new RedisGatewayBackplane(
                () => sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<RedisGatewayBackplane>>()));
            return services;
        }
    }
}
