using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Profilers.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddProfiler(this IServiceCollection services)
        {
            services.AddScoped<IProfiler, StackProfiler>();
            services.AddSingleton<IProfilerProvider, ProfilerProvider>();

            return services;
        }
    }
}
