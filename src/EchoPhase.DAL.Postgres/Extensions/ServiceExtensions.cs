using EchoPhase.DAL.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EchoPhase.DAL.Postgres.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddPostgres(this IServiceCollection services)
        {
            using (var serviceProvider = services.BuildServiceProvider())
            {
                var settings = serviceProvider
                    .GetRequiredService<IOptions<Configuration.Database.DatabaseOptions>>().Value.Postgres;

                services.AddDbContext<PostgresContext>(options =>
                    options.UseNpgsql(settings.ConnectionString, o =>
                    {
                        o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                        o.EnableRetryOnFailure(maxRetryCount: 3);
                        o.MigrationsHistoryTable("__EFMigrationsHistory", settings.Schema);
                    }));
            }

            services.AddScoped<WebHookRepository>();
            services.AddScoped<UserRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

            return services;
        }
    }
}
