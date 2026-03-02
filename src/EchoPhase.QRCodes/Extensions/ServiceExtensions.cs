using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.QRCodes.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddQRCodes(this IServiceCollection services)
        {
            services.AddSingleton<QRCodeService>();

            return services;
        }
    }
}
