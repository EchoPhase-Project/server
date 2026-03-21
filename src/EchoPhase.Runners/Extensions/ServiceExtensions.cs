// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

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

            services.AddSingleton<BlockHandlerResolver>();
            services.AddTransient<BlocksRunner>();

            return services;
        }
    }
}
