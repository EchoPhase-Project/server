// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Projection.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddProjection(this IServiceCollection services)
        {
            services.AddSingleton<Projector>();

            return services;
        }

        public static IServiceCollection AddProjection(this IServiceCollection services, ProjectionOptions options)
        {
            services.AddSingleton<Projector>(sp => new Projector(options));

            return services;
        }

        public static IServiceCollection AddProjection(this IServiceCollection services, Action<ProjectionOptions> configure)
        {
            ProjectionOptions options = new();
            configure(options);
            services.AddSingleton<Projector>(sp => new Projector(options));

            return services;
        }
    }
}
