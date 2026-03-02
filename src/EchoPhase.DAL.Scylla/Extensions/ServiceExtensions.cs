using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.DAL.Scylla.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddScylla(this IServiceCollection services)
        {
            services.AddScoped<ScyllaContext>();

            return services;
        }
    }

}
