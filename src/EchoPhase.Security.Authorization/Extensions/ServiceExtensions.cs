// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Authorization.Builders;
using EchoPhase.Security.Authorization.Handlers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

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
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            services.AddSingleton<IPolicyBuilder, RolePolicyBuilder>();
            services.AddSingleton<IPolicyBuilder, ScopePolicyBuilder>();
            services.AddSingleton<IPolicyBuilder, PermissionPolicyBuilder>();

            services.AddSingleton<IAuthorizationPolicyProvider, DynamicPolicyProvider>();

            return services;
        }
    }
}
