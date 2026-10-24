// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Database;
using EchoPhase.DAL.Redis.Interfaces;
using EchoPhase.DAL.Redis.Maintenance;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EchoPhase.DAL.Redis.Extensions
{
    public static class ServiceExtensions
    {
        private const string DataProtectionKeys = "DataProtection-Keys";

        /// <summary>
        /// One Redis connection for the application, opened on first use: the
        /// host can be built (CLI, OpenAPI export) without a reachable Redis.
        /// The distributed cache and Data Protection share it.
        /// </summary>
        public static IServiceCollection AddRedisCache(this IServiceCollection services)
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.Redis;
                var redis = ConnectionMultiplexer.Connect(settings.ConnectionString);
                if (!redis.IsConnected)
                    throw new InvalidOperationException("Failed to connect to Redis");
                return redis;
            });

            services.AddStackExchangeRedisCache(_ => { });
            services.AddOptions<RedisCacheOptions>()
                .Configure<IOptions<DatabaseOptions>, IServiceProvider>((options, database, sp) =>
                {
                    options.InstanceName = database.Value.Redis.InstanceName;
                    options.ConnectionMultiplexerFactory =
                        () => Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>());
                });

            // Maintenance mode: shared by every instance and the CLI.
            services.AddSingleton<IMaintenanceStore>(sp => new RedisMaintenanceStore(sp.GetRequiredService<IConnectionMultiplexer>));
            services.AddSingleton<MaintenanceSwitch>();

            string entryAssemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "EchoPhase";

            // What PersistKeysToStackExchangeRedis does, but resolving the
            // connection when keys are first needed instead of at registration.
            services.AddDataProtection().SetApplicationName(entryAssemblyName);
            services.AddOptions<KeyManagementOptions>()
                .Configure<IServiceProvider>((options, sp) => options.XmlRepository = new RedisXmlRepository(
                    () => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(), DataProtectionKeys));

            services.AddSingleton<ICacheContext, RedisContext>();

            services.AddDistributedMemoryCache();

            return services;
        }
    }
}
