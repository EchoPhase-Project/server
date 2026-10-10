// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Interceptors;
using EchoPhase.DAL.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EchoPhase.DAL.Postgres.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddPostgres(this IServiceCollection services)
        {
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<AuditInterceptor>();

            // Settings are read when a context is first configured, not here:
            // no second container, and registering needs no configuration.
            services.AddDbContext<PostgresContext>((sp, options) =>
                options
                    .UseNpgsql(sp.GetRequiredService<IOptions<Configuration.Database.DatabaseOptions>>().Value.Postgres.ConnectionString, o =>
                    {
                        o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                        o.EnableRetryOnFailure(maxRetryCount: 3);
                        o.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                    })
                    .AddInterceptors(sp.GetRequiredService<AuditInterceptor>())
                    // Other modules hook into saves by registering an IInterceptor.
                    .AddInterceptors(sp.GetServices<IInterceptor>()));

            services.AddScoped<WebHookRepository>();
            services.AddScoped<UserRepository>();
            services.AddScoped<SessionRepository>();
            services.AddScoped<ApiClientRepository>();
            services.AddScoped<ImpersonationRepository>();
            services.AddScoped<ConnectionRepository>();
            services.AddScoped<OAuthAppRepository>();
            services.AddScoped<UserPermissionGrantRepository>();
            services.AddScoped<WorkflowRepository>();

            return services;
        }
    }
}
