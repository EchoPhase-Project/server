using EchoPhase.Security.Authorization.Handlers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Microsoft.Extensions.Options;

namespace EchoPhase.Security.Authorization.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddAuthorizations(this IServiceCollection services)
        {
            services.AddSingleton<IAuthorizationHandler, RoleAuthorizationHandler>();
            services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();
            services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("Any", policy =>
                {
                    policy.RequireAuthenticatedUser();
                });

                options.AddPolicy("NoAccess", policy =>
                {
                    policy.RequireAssertion(context => false);
                });

                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(
                            IdentityConstants.ApplicationScheme,
                            JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            services.AddDynamicPolicyProvider();

            return services;
        }

        public static IServiceCollection AddDynamicPolicyProvider(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            var targets = assemblies.Length > 0
                ? assemblies
                : AppDomain.CurrentDomain.GetAssemblies();

            services.AddSingleton<IAuthorizationPolicyProvider>(sp =>
                new DynamicPolicyProvider(
                    sp.GetRequiredService<IOptions<AuthorizationOptions>>(),
                    sp,
                    targets));

            return services;
        }
    }
}
