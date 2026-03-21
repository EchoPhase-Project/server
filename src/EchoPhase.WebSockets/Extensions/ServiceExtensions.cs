// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using EchoPhase.WebSockets.Attributes;
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

            services.AddOpCodeHandlers();

            return services;
        }

        private static IServiceCollection AddOpCodeHandlers(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            var targets = assemblies.Length > 0
                ? assemblies
                : AppDomain.CurrentDomain.GetAssemblies();

            var types = targets
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    t is { IsClass: true, IsAbstract: false } &&
                    t.GetInterfaces().Any(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IOpCodeHandler<>)) &&
                    t.GetCustomAttribute<OpCodeHandlerAttribute>() is not null);

            foreach (var type in types)
            {
                var key = type.GetCustomAttribute<OpCodeHandlerAttribute>()!.OpCode;

                services.AddKeyedTransient(
                    typeof(IOpCodeHandler),
                    key,
                    (sp, _) => (IOpCodeHandler)ActivatorUtilities.CreateInstance(sp, type));
            }

            return services;
        }
    }
}
