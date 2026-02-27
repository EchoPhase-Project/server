using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using EchoPhase.Clients.Discord;
using EchoPhase.Configuration.Settings;
using EchoPhase.Configuration.Validators;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Redis;
using EchoPhase.DAL.Redis.Interfaces;
using EchoPhase.DAL.Scylla;
using EchoPhase.Identity;
using EchoPhase.Interfaces;
using EchoPhase.Profilers;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Roslyn;
using EchoPhase.Runners.Roslyn.Validators;
using EchoPhase.Scripting.Lexers;
using EchoPhase.Scripting.Parsers;
using EchoPhase.Scripting.Tokens;
using EchoPhase.Security.Antiforgery;
using EchoPhase.Security.Authentication;
using EchoPhase.Security.Authentication.Jwt;
using EchoPhase.Security.Authorization.Factories;
using EchoPhase.Security.Authorization.Handlers;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Security.Cryptography;
using EchoPhase.Security.Hashers;
using EchoPhase.Services;
using EchoPhase.Services.Events;
using EchoPhase.Types.Result.Extensions;
using EchoPhase.WebHooks;
using EchoPhase.WebSockets;
using EchoPhase.WebSockets.Processors;
using EchoPhase.WebSockets.Processors.Handlers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;

namespace EchoPhase.Extensions
{
    public static class ServiceExtensions
    {
        /*
        public static IServiceCollection AddTwitchClient(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddOptions<TwitchSettings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<TwitchSettings>, TwitchSettingsValidator>();

            services.AddHttpClient<TwitchClient>("Twitch", (serviceProvider, client) =>
            {
                var settings = serviceProvider
                    .GetRequiredService<IOptions<TwitchSettings>>().Value;

                client.BaseAddress = new Uri("https://api.twitch.tv/helix/");

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddPolicyHandler(request =>
                    request.Method == HttpMethod.Get ?
                        GetRetryPolicy() :
                        GetNoOpPolicy()
                );

            return services;
        }
        */

        public static IServiceCollection AddDiscordClient(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddOptions<DiscordSettings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<DiscordSettings>, DiscordSettingsValidator>();

            services.AddScoped<DiscordTokenRepository>();
            services.AddScoped<IDiscordTokenService, DiscordTokenService>();

            services.AddHttpClient<DiscordClient>("Discord", (serviceProvider, client) =>
                    {
                        var settings = serviceProvider
                            .GetRequiredService<IOptions<DiscordSettings>>().Value;

                        client.BaseAddress = new Uri("https://discord.com/api/v10/");

                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddPolicyHandler(request =>
                    request.Method == HttpMethod.Get ?
                        GetRetryPolicy() :
                        GetNoOpPolicy()
                );

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
            HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        private static IAsyncPolicy<HttpResponseMessage> GetNoOpPolicy() =>
            Policy
                .NoOpAsync()
                .AsAsyncPolicy<HttpResponseMessage>();

        public static IServiceCollection AddAppSettings(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddOptions<AppSettings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<AppSettings>, AppSettingsValidator>();

            return services;
        }

        public static IServiceCollection AddPostgres(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.Configure<PostgresSettings>(options =>
            {
                var pgHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
                var pgPort = Environment.GetEnvironmentVariable("POSTGRES_PORT");
                var pgPw = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
                var pgUser = Environment.GetEnvironmentVariable("POSTGRES_USER");
                var pgDb = Environment.GetEnvironmentVariable("POSTGRES_DB");

                if (!string.IsNullOrEmpty(pgHost) &&
                    !string.IsNullOrEmpty(pgPort) &&
                    !string.IsNullOrEmpty(pgPw) &&
                    !string.IsNullOrEmpty(pgUser) &&
                    !string.IsNullOrEmpty(pgDb))
                {
                    options.ConnectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPw}";
                }
            });

            services.AddOptions<PostgresSettings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<PostgresSettings>, PostgresSettingsValidator>();

            var serviceProvider = services.BuildServiceProvider();
            var settings = serviceProvider
                .GetRequiredService<IOptions<PostgresSettings>>().Value;

            services.AddDbContext<PostgresContext>(options =>
                options
                    .UseNpgsql(settings.ConnectionString,
                        o =>
                        {
                            o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                            o.EnableRetryOnFailure(maxRetryCount: 3);
                        }));

            return services;
        }

        public static IServiceCollection AddScylla(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.Configure<ScyllaSettings>(options =>
            {
                var contactPoint = Environment.GetEnvironmentVariable("SCYLLA_CONTACT_POINT");
                var keyspace = Environment.GetEnvironmentVariable("SCYLLA_KEYSPACE");
                var username = Environment.GetEnvironmentVariable("SCYLLA_USERNAME");
                var password = Environment.GetEnvironmentVariable("SCYLLA_PASSWORD");

                if (!string.IsNullOrWhiteSpace(contactPoint))
                    options.ContactPoint = contactPoint;

                if (!string.IsNullOrWhiteSpace(keyspace))
                    options.Keyspace = keyspace;

                if (!string.IsNullOrWhiteSpace(username))
                    options.Username = username;

                if (!string.IsNullOrWhiteSpace(password))
                    options.Password = password;
            });

            services.AddOptions<ScyllaSettings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<ScyllaSettings>, ScyllaSettingsValidator>();

            services.AddScoped<ScyllaContext>();

            return services;
        }

        public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.Configure<RedisSettings>(options =>
            {
                var valkeyHost = Environment.GetEnvironmentVariable("VALKEY_HOST");
                var valkeyPort = Environment.GetEnvironmentVariable("VALKEY_PORT");
                var valkeyPw = Environment.GetEnvironmentVariable("VALKEY_PASSWORD");
                var valkeySsl = Environment.GetEnvironmentVariable("VALKEY_SSL");
                var valkeyTimeout = Environment.GetEnvironmentVariable("VALKEY_TIMEOUT");

                if (!string.IsNullOrEmpty(valkeyHost) && !string.IsNullOrEmpty(valkeyPort))
                {
                    var parts = new List<string>
                    {
                        $"{valkeyHost}:{valkeyPort}"
                    };

                    if (!string.IsNullOrWhiteSpace(valkeyPw))
                        parts.Add($"password={valkeyPw}");

                    if (bool.TryParse(valkeySsl, out var sslRes))
                        parts.Add($"ssl={sslRes}");

                    if (int.TryParse(valkeyTimeout, out var timeoutRes))
                        parts.Add($"connectTimeout={timeoutRes}");

                    options.ConnectionString = string.Join(", ", parts);
                }
            });

            services.AddOptions<RedisSettings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<RedisSettings>, RedisSettingsValidator>();

            var serviceProvider = services.BuildServiceProvider();
            var settings = serviceProvider
                .GetRequiredService<IOptions<RedisSettings>>().Value;

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = settings.ConnectionString;
                options.InstanceName = settings.InstanceName;
            });

            var redis = ConnectionMultiplexer.Connect(settings.ConnectionString);
            if (!redis.IsConnected)
                throw new InvalidOperationException("Failed to connect to Redis");

            services.AddSingleton<IConnectionMultiplexer>(redis);

            string entryAssemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "EchoPhase";

            services.AddDataProtection()
                .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys")
                .SetApplicationName(entryAssemblyName);

            services.AddSingleton<ICacheContext, RedisContext>();
            services.AddTransient<IKeyVault, KeyVault>();

            services.AddDistributedMemoryCache();


            return services;
        }

        public static IServiceCollection AddPreparedMvc(this IServiceCollection services)
        {
            services.Configure<RazorViewEngineOptions>(o =>
            {
                // {2} is area, {1} is controller,{0} is the action
                o.ViewLocationFormats.Clear();
                o.ViewLocationFormats.Add("/Controllers/{1}/Views/{0}" + RazorViewEngine.ViewExtension);
                o.ViewLocationFormats.Add("/Controllers/Shared/{0}" + RazorViewEngine.ViewExtension);
                o.ViewLocationFormats.Add("/Controllers/Shared/Views/{0}" + RazorViewEngine.ViewExtension);

                o.AreaViewLocationFormats.Clear();
                o.AreaViewLocationFormats.Add("/Areas/{2}/Controllers/{1}/Views/{0}" + RazorViewEngine.ViewExtension);
                o.AreaViewLocationFormats.Add("/Areas/{2}/Controllers/Shared/Views/{0}" + RazorViewEngine.ViewExtension);
                o.AreaViewLocationFormats.Add("/Areas/Shared/Views/{0}" + RazorViewEngine.ViewExtension);
            });

            services.AddMvc()
                //.AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
                //.AddDataAnnotationsLocalization()
                //.AddRazorRuntimeCompilation()
                .AddNewtonsoftJson(options =>
                    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                );

            return services;
        }

        public static IServiceCollection AddCorsPolicies(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());

                options.AddPolicy("AllowSpecificOrigin",
                    builder => builder
                        .WithOrigins("https://twitch.app")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials());

                options.AddPolicy("AllowLocalhost",
                    builder => builder
                        .WithOrigins("https://localhost")
                        .AllowAnyHeader()
                        .AllowAnyMethod());

                options.AddPolicy("AllowAllGrpc",
                    builder => builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding"));
            });

            return services;
        }

        public static IServiceCollection AddLocalizationOptions(this IServiceCollection services)
        {
            services.AddLocalization(options => options.ResourcesPath = "Resources");

            services.Configure<RequestLocalizationOptions>(options =>
            {
                string defaultCulture = "en";

                var supportedCultures = new[] { defaultCulture, "ru", "ro" };
                var cultures = supportedCultures.Select(culture => new CultureInfo(culture)).ToList();

                options.DefaultRequestCulture = new RequestCulture(defaultCulture);
                options.SupportedCultures = cultures;
                options.SupportedUICultures = cultures;

                options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
            });

            return services;
        }

        public static IServiceCollection AddAntiforgeryOptions(this IServiceCollection services)
        {
            services.AddAntiforgery(options =>
            {
                options.FormFieldName = AntiforgeryService.CsrfFormName;
                options.Cookie.Name = AntiforgeryService.CsrfCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // change to Always
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.HeaderName = AntiforgeryService.CsrfHeaderName;
                options.SuppressXFrameOptionsHeader = false;
            });

            services.AddScoped<IAntiforgeryService, AntiforgeryService>();

            return services;
        }

        public static IServiceCollection AddRoles(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddScoped<RoleManager<UserRole>>();
            services.AddScoped<IRoleService, RoleService>();

            return services;
        }

        public static IServiceCollection AddAuthentication(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddOptions<AuthenticationSettings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<AuthenticationSettings>, AuthenticationSettingsValidator>();

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
                LoginPath = "/app/login",
                LogoutPath = "/app/logout",
                AccessDeniedPath = "/error/access-denied",
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
                        .GetRequiredService<IOptions<AuthenticationSettings>>().Value.Schemes.Bearer;

                    var keysService = serviceProvider
                        .GetRequiredService<IKeyVault>();

                    var result = keysService.GetOrSet(settings.Key);

                    result.OnFailure(err =>
                        throw new InvalidOperationException(err.Value));

                    if (!result.TryGetValue(out var key))
                        throw new InvalidOperationException($"Missing '{settings.Key}' key.");

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
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
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IRefreshTokenProvider, RefreshTokenProvider>();
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }

        public static IServiceCollection AddEventService(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddHttpClient();

            services.AddScoped<WebHookRepository>();
            services.AddScoped<WebHookService>();

            services.AddOptions<WebSocketSettings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<WebSocketSettings>, WebSocketSettingsValidator>();

            services.AddSingleton<WebSocketConnectionManager>();
            services.AddScoped<WebSocketService>();

            services.AddSingleton<OpCodeHandlerResolver>();
            services.AddScoped<WebSocketProcessor>();

            services.AddScoped<IEventService, EventService>();

            return services;
        }

        public static IServiceCollection AddAes(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddOptions<AesSettings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<AesSettings>, AesSettingsValidator>();

            services.AddSingleton<AesGcm>();

            return services;
        }

        public static IServiceCollection AddPasswordHasher(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddOptions<Argon2Settings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Argon2Settings>, Argon2SettingsValidator>();

            services.AddSingleton<IPasswordHasher<User>, Argon2Hasher>();

            return services;
        }

        public static IServiceCollection AddRunners(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddOptions<RunnersSettings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<RunnersSettings>, RunnersSettingsValidator>();

            services.AddSingleton<ISecurityValidator, SecurityValidator>();
            services.AddTransient<RoslynRunner>();

            services.AddSingleton<BlockHandlerResolver>();
            services.AddTransient<BlocksRunner>();

            return services;
        }

        public static IServiceCollection AddPolicies(this IServiceCollection services)
        {
            services.AddSingleton<IRolesBitMask, RolesBitMask>();
            services.AddSingleton<IIntentsBitMask, IntentsBitMask>();
            services.AddSingleton<IPermissionsBitMask, PermissionsBitMask>();

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

        public static IServiceCollection AddCrypto25519(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddOptions<Crypto25519Settings>()
                .Bind(configurationSection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<Crypto25519Settings>, Crypto25519SettingsValidator>();

            services.AddSingleton<ICrypto25519, Crypto25519>();
            return services;
        }

        public static IServiceCollection AddExpressionEval(this IServiceCollection services)
        {
            services.AddScoped<ILexer<Token>, Lexer>();
            services.AddScoped<ILexer<TemplateToken>, TemplateLexer>();
            services.AddScoped<IParser<Token>, Parser>();
            services.AddScoped<IParser<TemplateToken>, TemplateParser>();
            services.AddScoped<ILexer<PathToken>, PathLexer>();
            services.AddScoped<IPathParser<PathToken>, PathParser>();

            return services;
        }

        public static IServiceCollection AddProfiler(this IServiceCollection services)
        {
            services.AddScoped<IProfiler, StackProfiler>();
            services.AddSingleton<IProfilerProvider, ProfilerProvider>();

            return services;
        }
    }
}
