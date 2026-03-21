// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.DAL.Scylla.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddScylla(this IServiceCollection services)
        {
            services.AddScoped<ScyllaContext>();

            return services;
        }
    }

}
