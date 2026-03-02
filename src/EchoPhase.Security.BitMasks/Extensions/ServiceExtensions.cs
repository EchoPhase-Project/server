using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.BitMasks.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddBitMasks(this IServiceCollection services)
        {
            services.AddSingleton<IRolesBitMask, RolesBitMask>();
            services.AddSingleton<IIntentsBitMask, IntentsBitMask>();
            services.AddSingleton<IIntentsBitMask, IntentsBitMask>();
            services.AddSingleton<IPermissionsBitMask, PermissionsBitMask>();

            return services;
        }
    }
}
