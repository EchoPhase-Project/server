using EchoPhase.DAL.Redis.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EchoPhase.DAL.Redis.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddRedisCache(this IServiceCollection services)
        {

            var serviceProvider = services.BuildServiceProvider();
            var settings = serviceProvider
                .GetRequiredService<IOptions<Configuration.Database.DatabaseOptions>>().Value.Redis;

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = settings.ConnectionString;
                options.InstanceName = settings.InstanceName;
            });

            var redis = ConnectionMultiplexer.Connect(settings.ConnectionString);
            if (!redis.IsConnected)
                throw new InvalidOperationException("Failed to connect to Redis");

            services.AddSingleton<IConnectionMultiplexer>(redis);

            string entryAssemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "EchoPhase";

            services.AddDataProtection()
                .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys")
                .SetApplicationName(entryAssemblyName);

            services.AddSingleton<ICacheContext, RedisContext>();

            services.AddDistributedMemoryCache();


            return services;
        }
    }
}
