using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Scheduling.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddScheduling(this IServiceCollection services)
        {
            services.AddSingleton<DelayedTaskScheduler>();

            return services;
        }
    }
}
