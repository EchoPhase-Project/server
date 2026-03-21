// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Processors;
using EchoPhase.WebSockets.Processors.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.WebSockets.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddWebSocket(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddSingleton<WebSocketConnectionManager>();
            services.AddScoped<WebSocketService>();
            services.AddScoped<WebSocketProcessor>();
            services.AddSingleton<OpCodeHandlerResolver>();

            services.AddKeyedTransient<IOpCodeHandler, HandshakeHandler>(OpCodes.Handshake);
            services.AddKeyedTransient<IOpCodeHandler, DisconnectHandler>(OpCodes.Disconnect);
            services.AddKeyedTransient<IOpCodeHandler, PingHandler>(OpCodes.Ping);
            services.AddKeyedTransient<IOpCodeHandler, AdjustHandler>(OpCodes.Adjust);

            return services;
        }
    }
}
