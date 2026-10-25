// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Storage.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Files.Extensions
{
    public static class ServiceExtensions
    {
        /// <summary>Users' files (<see cref="FileService"/>) on object storage.</summary>
        public static IServiceCollection AddFiles(this IServiceCollection services)
        {
            services.AddObjectStorage();
            services.AddScoped<FileService>();
            return services;
        }
    }
}
