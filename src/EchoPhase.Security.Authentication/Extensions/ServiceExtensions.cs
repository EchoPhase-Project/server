using Microsoft.Extensions.DependencyInjection;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using EchoPhase.Security.Authentication.Jwt;
using EchoPhase.Security.Cryptography.Vaults;
using Microsoft.AspNetCore.Identity;
using EchoPhase.Types.Result.Extensions;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using EchoPhase.Identity;

namespace EchoPhase.Security.Authentication.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddAuthentications(this IServiceCollection services)
        {
            services.AddScoped<SignInManager<User>, SignInManager<User>>();
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
            })
                .AddEntityFrameworkStores<PostgresContext>()
                .AddDefaultTokenProviders();

            CookieAuthenticationOptions authCookie = new CookieAuthenticationOptions()
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
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
                {
                    var serviceProvider = services.BuildServiceProvider();
                    var settings = serviceProvider
                        .GetRequiredService<IOptions<Configuration.Authentication.AuthenticationOptions>>().Value.Bearer;

                    var keysService = serviceProvider
                        .GetRequiredService<IKeyVault>();

                    var result = keysService.GetOrSet(settings.Key);

                    result.OnFailure(err =>
                        throw new InvalidOperationException(err.Value));

                    if (!result.TryGetValue(out var key))
                        throw new InvalidOperationException($"Missing '{settings.Key}' key.");

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        AuthenticationType = JwtBearerDefaults.AuthenticationScheme,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = settings.ValidIssuer,
                        ValidAudiences = settings.ValidAudiences,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ClockSkew = TimeSpan.Zero,
                        NameClaimType = JwtRegisteredClaimNames.Name,
                        RoleClaimType = RoleService.RoleClaim
                    };
                })
            .AddCookie(options =>
                options.CopyFrom(authCookie));

            services.AddScoped<IJwtTokenProvider, JwtTokenProvider>();
            services.AddScoped<IRefreshTokenProvider, RefreshTokenProvider>();
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}
