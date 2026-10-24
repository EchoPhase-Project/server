// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Runner;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Http;
using EchoPhase.Runners.Blocks.Validation;
using EchoPhase.Runners.Roslyn;
using EchoPhase.Runners.Roslyn.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EchoPhase.Runners.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddRunners(this IServiceCollection services)
        {
            services.AddSingleton<ISecurityValidator, SecurityValidator>();
            services.AddTransient<RoslynRunner>();

            // Every [BlockType] handler in this assembly: types, versions, outputs.
            var registry = BlockTypeRegistry.FromAssembly(typeof(BlocksRunner).Assembly);
            services.AddSingleton(registry);
            registry.RegisterHandlers(services);
            services.AddSingleton<BlockProgramValidator>();
            services.AddSingleton<BlockTypeCatalog>();

            services.AddSingleton<BlockHandlerResolver>();
            // Observers (IBlockRunObserver) are optional: register any number.
            services.TryAddSingleton(TimeProvider.System);
            services.AddLogging();
            services.AddTransient<BlocksRunner>();

            // HttpRequest: public addresses only (checked per connection), no
            // proxy (it would dial for us), no cookies shared between programs.
            services.AddHttpClient(HttpRequestHandler.ClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(sp =>
                {
                    var http = sp.GetRequiredService<IOptions<RunnerOptions>>().Value.Blocks.Http;
                    return new SocketsHttpHandler
                    {
                        ConnectCallback = PublicNetworkConnector.Create(http.AllowPrivateNetworks),
                        UseProxy = false,
                        UseCookies = false,
                        AllowAutoRedirect = http.MaxRedirects > 0,
                        MaxAutomaticRedirections = Math.Max(1, http.MaxRedirects),
                        AutomaticDecompression = System.Net.DecompressionMethods.All,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
                    };
                });

            return services;
        }
    }
}
