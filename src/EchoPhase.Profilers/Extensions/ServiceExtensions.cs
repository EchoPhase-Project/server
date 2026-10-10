// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EchoPhase.Profilers.Extensions
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// One profiler per scope (request, block run), off unless
        /// Profiling:Enabled; see <see cref="ProfilerOptions"/>.
        /// </summary>
        public static IServiceCollection AddProfiler(this IServiceCollection services)
        {
            // Bound when the host has configuration; a bare container (tests,
            // tools) gets the defaults: off.
            services.AddOptions<ProfilerOptions>().Configure<IServiceProvider>((options, sp) =>
                sp.GetService<IConfiguration>()?.GetSection(ProfilerOptions.SectionName).Bind(options));
            services.AddScoped<IProfiler>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ProfilerOptions>>().Value;
                var logs = sp.GetService<ILoggerFactory>();
                return options.Mode == ProfilerMode.Flat
                    ? new FlatProfiler(options.Enabled, logs?.CreateLogger<FlatProfiler>())
                    : new StackProfiler(options.Enabled, logs?.CreateLogger<StackProfiler>());
            });
            return services;
        }
    }
}
