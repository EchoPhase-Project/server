using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Projection.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddProjection(this IServiceCollection services)
        {
            services.AddSingleton<Projector>();

            return services;
        }
    }
}
