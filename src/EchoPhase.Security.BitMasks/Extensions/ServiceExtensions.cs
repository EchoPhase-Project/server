using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.BitMasks.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddBitMasks(this IServiceCollection services)
        {
            services.AddSingleton<RolesBitMask>();
            services.AddSingleton<ScopesBitMask>();
            services.AddSingleton<IntentsBitMask>();
            services.AddSingleton<ResourcePermissionsBitMask>();

            return services;
        }
    }
}
