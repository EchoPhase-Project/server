using EchoPhase.Security.Antiforgery.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.Antiforgery.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddConfiguredAntiforgery(this IServiceCollection services)
        {
            services.AddScoped<IAntiforgeryService, AntiforgeryService>();
            services.AddScoped<ValidateAntiForgeryFilter>();

            return services;
        }
    }
}
