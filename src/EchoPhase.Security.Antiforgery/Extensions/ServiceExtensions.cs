using Microsoft.Extensions.DependencyInjection;
using EchoPhase.Security.Antiforgery.Filters;
using Microsoft.AspNetCore.Http;

namespace EchoPhase.Security.Antiforgery.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddConfiguredAntiforgery(this IServiceCollection services)
        {
            services.AddAntiforgery(options =>
            {
                options.FormFieldName = AntiforgeryService.CsrfFormName;
                options.Cookie.Name = AntiforgeryService.CsrfCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.HeaderName = AntiforgeryService.CsrfHeaderName;
                options.SuppressXFrameOptionsHeader = false;
            });

            services.AddScoped<IAntiforgeryService, AntiforgeryService>();
            services.AddScoped<BearerOrValidateAntiForgeryTokenFilter>();

            return services;
        }
    }
}
