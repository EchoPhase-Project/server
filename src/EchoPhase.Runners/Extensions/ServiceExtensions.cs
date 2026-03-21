// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Roslyn;
using EchoPhase.Runners.Roslyn.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddRunners(this IServiceCollection services)
        {
            services.AddSingleton<ISecurityValidator, SecurityValidator>();
            services.AddTransient<RoslynRunner>();

            services.AddBlockHandlers();

            services.AddSingleton<BlockHandlerResolver>();
            services.AddTransient<BlocksRunner>();

            return services;
        }

        private static IServiceCollection AddBlockHandlers(
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
                        i.GetGenericTypeDefinition() == typeof(IBlockHandler<>)) &&
                    t.GetCustomAttribute<BlockTypeHandlerAttribute>() is not null);

            foreach (var type in types)
            {
                var key = type.GetCustomAttribute<BlockTypeHandlerAttribute>()!.Type;

                services.AddKeyedTransient(
                    typeof(IBlockHandler),
                    key,
                    (sp, _) => (IBlockHandler)ActivatorUtilities.CreateInstance(sp, type));
            }

            return services;
        }
    }
}
