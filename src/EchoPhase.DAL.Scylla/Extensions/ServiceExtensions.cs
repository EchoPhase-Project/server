// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using EchoPhase.Configuration.Database;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Loggers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EchoPhase.DAL.Scylla.Extensions
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// One connection for the application (created on first use, closed on
        /// shutdown); contexts on top of it are scoped and disposed per scope.
        /// </summary>
        /// <param name="migrationAssemblies">
        /// Assemblies holding <c>IMigration</c> types; defaults to the ORM's own.
        /// </param>
        public static IServiceCollection AddScylla(this IServiceCollection services, params Assembly[] migrationAssemblies)
        {
            services.AddSingleton(sp => new ScyllaConnection(
                sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.Scylla,
                new LoggerQueryLogger(sp.GetRequiredService<ILogger<ScyllaConnection>>()),
                migrationAssemblies.Length > 0 ? migrationAssemblies : null));

            services.AddScoped<ScyllaContext>();

            return services;
        }
    }

}
