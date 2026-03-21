// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Antiforgery.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.Antiforgery.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddConfiguredAntiforgery(this IServiceCollection services)
        {
            services.AddScoped<IAntiforgeryService, AntiforgeryService>();
            services.AddScoped<ValidateAntiForgeryFilter>();

            return services;
        }
    }
}
