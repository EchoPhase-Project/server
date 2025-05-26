using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Net.Http.Headers;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using Polly;
using Polly.Extensions.Http;

using EchoPhase.Roles;
using EchoPhase.Models;
using EchoPhase.Clients;
using EchoPhase.Validators;
using EchoPhase.Repositories;
using EchoPhase.Interfaces;
using EchoPhase.Processors;
using EchoPhase.DAL.Redis;
using EchoPhase.DAL.Postgres;
using EchoPhase.Services;
using EchoPhase.Services.Events;
using EchoPhase.Services.Security;
using EchoPhase.Services.WebHooks;
using EchoPhase.Services.WebSockets;
using EchoPhase.Configurations.Models;

namespace EchoPhase.Extensions
{
	public static class ServiceExtensions
	{
		public static IServiceCollection AddTwitchClient(this IServiceCollection services, IConfigurationSection configurationSection)
		{
			services.AddOptions<TwitchSettings>()
				.Bind(configurationSection)
				.ValidateDataAnnotations()
				.ValidateOnStart();

			services.AddSingleton<IValidateOptions<TwitchSettings>, TwitchSettingsValidator>();

			var httpClientBuilder = services.AddHttpClient<TwitchClient>("Twitch", (serviceProvider, client) =>
			{
				var settings = serviceProvider
					.GetRequiredService<IOptions<TwitchSettings>>().Value;

				client.BaseAddress = new Uri("https://api.twitch.tv/helix/");

				client.DefaultRequestHeaders.Add("Client-Id", settings.ClientId);
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			})
				.SetHandlerLifetime(TimeSpan.FromMinutes(5));

			httpClientBuilder
				.AddPolicyHandler(request => 
					request.Method == HttpMethod.Get ? 
						GetRetryPolicy() : 
						GetNoOpPolicy()
				);

			return services;
		}

		public static IServiceCollection AddDiscordClient(this IServiceCollection services, IConfigurationSection configurationSection)
		{
			services.AddOptions<DiscordSettings>()
				.Bind(configurationSection)
				.ValidateDataAnnotations()
				.ValidateOnStart();

			services.AddSingleton<IValidateOptions<DiscordSettings>, DiscordSettingsValidator>();

			services.AddScoped<DiscordTokenRepository>();
			services.AddScoped<IDiscordTokenService, DiscordTokenService>();

			var httpClientBuilder = services.AddHttpClient<DiscordClient>("Discord", (serviceProvider, client) =>
					{
				var settings = serviceProvider
					.GetRequiredService<IOptions<DiscordSettings>>().Value;

				client.BaseAddress = new Uri("https://discord.com/api/v10/");

				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", settings.Token);
				client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			})
			.SetHandlerLifetime(TimeSpan.FromMinutes(5));

			httpClientBuilder
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

		private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy() =>
			Policy
				.TimeoutAsync<HttpResponseMessage>(10);

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

		public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfigurationSection configurationSection)
		{
			services.AddOptions<RedisSettings>()
				.Bind(configurationSection)
				.ValidateDataAnnotations()
				.ValidateOnStart();

			services.AddSingleton<IValidateOptions<RedisSettings>, RedisSettingsValidator>();

			services.AddStackExchangeRedisCache(options =>
			{
				var serviceProvider = services.BuildServiceProvider();
				var settings = serviceProvider
					.GetRequiredService<IOptions<RedisSettings>>().Value;

				options.Configuration = settings.ConnectionString;
				options.InstanceName = settings.InstanceName;
			});

			services.AddSingleton<ICacheContext, RedisContext>();

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
				options.FormFieldName = "Antiforgery";
				options.HeaderName = "X-CSRF-TOKEN";
				options.SuppressXFrameOptionsHeader = false;
			});

			return services;
		}

		public static IServiceCollection AddRoles(this IServiceCollection services, IConfigurationSection configurationSection, params string[] roles)
		{
			services.AddOptions<RoleSettings>()
				.Bind(configurationSection)
				.ValidateDataAnnotations()
				.ValidateOnStart();

			services.AddSingleton<IValidateOptions<RoleSettings>, RoleSettingsValidator>();

			services.AddScoped<RoleManager<UserRole>>();
			services.AddScoped<IRoleService, RoleService>();

			var serviceProvider = services.BuildServiceProvider();
			var settings = serviceProvider.GetRequiredService<IOptions<RoleSettings>>().Value;
			if (!settings.CheckRoles)
				return services;

			var scope = serviceProvider.CreateScope();
			var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
			foreach (var role in roles)
			{
				roleService.CreateRoleAsync(role).GetAwaiter().GetResult();
			}

			return services;
		}

		public static IServiceCollection AddAuthentication(this IServiceCollection services, IConfigurationSection configurationSection)
		{
			services.AddOptions<JwtSettings>()
				.Bind(configurationSection)
				.ValidateDataAnnotations()
				.ValidateOnStart();

			services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

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
						.GetRequiredService<IOptions<JwtSettings>>().Value;

					var key = Encoding.ASCII.GetBytes(settings.SecretKey);

					options.TokenValidationParameters = new TokenValidationParameters
					{
						ValidateIssuer = true,
						ValidateAudience = true,
						ValidateLifetime = true,
						ValidateIssuerSigningKey = true,
						ValidIssuer = settings.Issuer,
						ValidAudience = settings.Audience,
						IssuerSigningKey = new SymmetricSecurityKey(key),
						ClockSkew = TimeSpan.Zero
					};
				})
			.AddCookie(options => 
				options.CopyFrom(authCookie));

			services.AddScoped<IJwtTokenService, JwtTokenService>();
			
			return services;
		}

		public static IServiceCollection AddEventService(this IServiceCollection services)
		{
			services.AddHttpClient();

			services.AddScoped<WebHookRepository>();
			services.AddScoped<WebHookService>();

			services.AddSingleton<WebSocketConnectionManager>();
			services.AddScoped<WebSocketService>();
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

			services.AddSingleton<AesService>();

			return services;
		}
	}
}
