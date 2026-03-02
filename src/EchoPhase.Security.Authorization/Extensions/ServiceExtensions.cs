using Microsoft.Extensions.DependencyInjection;
using EchoPhase.Security.Authorization.Factories;
using EchoPhase.Security.Authorization.Handlers;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Security.Authorization.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddAuthorizations(this IServiceCollection services)
        {
            services.AddSingleton<IRolesFactory, RolesFactory>();
            services.AddSingleton<IPermissionsFactory, PermissionsFactory>();

            services.AddSingleton<IAuthorizationHandler, PermissionsAuthorizationHandler>();
            services.AddSingleton<IAuthorizationHandler, RolesAuthorizationHandler>();

            services.AddAuthorization(options =>
            {
                var provider = services.BuildServiceProvider();
                var permissionFactory = provider.GetRequiredService<IPermissionsFactory>();
                var roleFactory = provider.GetRequiredService<IRolesFactory>();

                options.AddPolicy("AdminOnly", policy =>
                {
                    var req = roleFactory.Requirement(
                        Roles.Admin
                    );

                    policy.Requirements.Add(req);
                });

                options.AddPolicy("DevOrHigher", policy =>
                {
                    var req = roleFactory.Requirement(
                        Roles.Admin, Roles.Dev
                    );

                    policy.Requirements.Add(req);
                });

                options.AddPolicy("TrustedOnly", policy =>
                {
                    var req = roleFactory.Requirement(
                        Roles.Admin, Roles.Dev, Roles.Staff
                    );

                    policy.Requirements.Add(req);
                });

                options.AddPolicy("Any", policy =>
                {
                    policy.RequireAuthenticatedUser();
                });

                options.AddPolicy("NoAccess", policy =>
                {
                    policy.RequireAssertion(context => false);
                });

                options.AddPolicy("CanEdit", policy =>
                {
                    var req = permissionFactory.Requirement(
                        (Resources.User, new[] { Permissions.Add, Permissions.Edit }),
                        (Resources.WebSocket, new[] { Permissions.Connect, Permissions.Execute }),
                        (Resources.Tokens, new[] { Permissions.Import, Permissions.Export })
                    );

                    policy.Requirements.Add(req);
                });

                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(
                            IdentityConstants.ApplicationScheme,
                            JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            return services;
        }
    }
}
