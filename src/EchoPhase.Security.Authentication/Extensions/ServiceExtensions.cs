// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Authentication.Jwt;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Jwt.Claims.Providers;
using EchoPhase.Security.Authentication.Jwt.Providers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.Authentication.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddAuthentications(this IServiceCollection services)
        {
            services.AddScoped<IUserPrincipalFactory, UserPrincipalFactory>();
            services.AddScoped<RefreshSignInManager>();
            services.AddIdentity<User, UserRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;

                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                options.User.AllowedUserNameCharacters =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                options.User.RequireUniqueEmail = false;

                options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
                options.ClaimsIdentity.UserIdClaimType = JwtRegisteredClaimNames.Sub;
            })
                .AddEntityFrameworkStores<PostgresContext>()
                .AddDefaultTokenProviders();

            var authCookie = new CookieAuthenticationOptions()
            {
                LoginPath = "/login",
                LogoutPath = "/logout",
                AccessDeniedPath = "/403",
                ExpireTimeSpan = TimeSpan.FromMinutes(60),
                SlidingExpiration = true
            };

            authCookie.Cookie.HttpOnly = true;

            services.ConfigureApplicationCookie(options =>
                options.CopyFrom(authCookie));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddRefresh()
                .AddCookie(options =>
                    options.CopyFrom(authCookie));

            services.AddScoped<IJwtTokenProvider, JwtTokenProvider>();
            services.AddScoped<IRefreshTokenProvider, RefreshTokenProvider>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();

            services.AddScoped<IClaimsProvider, RolesClaimsProvider>();
            services.AddScoped<IClaimsProvider, ScopesClaimsProvider>();
            services.AddScoped<IClaimsProvider, IntentsClaimsProvider>();
            services.AddScoped<IClaimsProvider, ResourcePermissionsClaimsProvider>();

            return services;
        }
    }
}
