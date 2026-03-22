// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using EchoPhase.Clients.Extensions;
using EchoPhase.Configuration.Extensions;
using EchoPhase.DAL.Postgres.Extensions;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Redis.Extensions;
using EchoPhase.DAL.Scylla.Extensions;
using EchoPhase.Extensions;
using EchoPhase.Helpers;
using EchoPhase.Hubs;
using EchoPhase.Hubs.Managers;
using EchoPhase.Identity;
using EchoPhase.Interfaces.Services;
using EchoPhase.Middlewares;
using EchoPhase.Profilers.Extensions;
using EchoPhase.Projection.Extensions;
using EchoPhase.QRCodes.Extensions;
using EchoPhase.RouteConstraints;
using EchoPhase.Runners.Extensions;
using EchoPhase.Scheduling.Extensions;
using EchoPhase.Scripting.Extensions;
using EchoPhase.Security.Antiforgery.Extensions;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Extensions;
using EchoPhase.Security.BitMasks.Extensions;
using EchoPhase.Security.Cryptography.Extensions;
using EchoPhase.Security.Hashers.Extensions;
using EchoPhase.WebSockets.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace EchoPhase
{
    /// <summary>
    /// Configures the application's services and dependencies.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class with the specified configuration.
        /// </summary>
        /// <param name="configuration">Application configuration properties.</param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Gets the application configuration properties.
        /// </summary>
        public IConfiguration Configuration
        {
            get;
        }

        /// <summary>
        /// Configures the services collection for dependency injection.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
                builder.AddDebug();
                builder.AddFile("Logs/logs-{Date}.txt");
            });

            services.Configure<RouteOptions>(options =>
            {
                options.ConstraintMap.Add("ulong", typeof(ULongRouteConstraint));
                options.ConstraintMap.Add("username", typeof(UsernameRouteConstraint));
            });

            services.AddSingleton(TimeZoneInfo.Utc);

            services.AddSwaggerGen(o =>
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                o.IncludeXmlComments(xmlPath);
                o.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "EchoPhase API",
                    Description = "API v1",
                    TermsOfService = new Uri("https://echophase/terms"),
                    Contact = new OpenApiContact
                    {
                        Name = "Labo Daniil",
                        Email = "extremal.user@gmail.com",
                        Url = new Uri("https://github.com/CodeHeister"),
                    },
                    License = new OpenApiLicense
                    {
                        Name = "BSD 3-Clause",
                        Url = new Uri("https://opensource.org/licenses/BSD-3-Clause"),
                    }
                });
            });

            services.AddHttpContextAccessor();

            services.AddPreparedMvc();
            services.AddCorsPolicies();
            services.AddConfiguredAntiforgery();
            services.AddLocalizationOptions();
            services.AddConfigurations();

            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 102400000;
            });

            services.AddSingleton<IStringLocalizerFactory, ResourceManagerStringLocalizerFactory>();
            services.AddSingleton<IUserConnectionManager, UserConnectionManager>();

            services.AddSingleton<FileHelper>();
            services.AddProjection();

            services.AddCryptography();
            services.AddPasswordHasher();
            services.AddBitMasks();

            services.AddQRCodes();

            services.AddScoped<IUserService, UserService>();

            services.AddScoped<UserManager<User>, UserManager<User>>();

            services.AddScripting();
            services.AddProfiler();

            services.AddRunners();

            services.AddScheduling();

            services.AddWebSocket();
            services.AddEventService();

            services.AddGrpc();

            services.AddPostgres();
            services.AddScylla();
            services.AddRedisCache();

            services.AddRoles();

            services.AddAuthentications();
            services.AddAuthorizations();
            services.AddProblemDetails();

            //services.AddTwitchClient(Configuration.GetSection("Twitch"));
            services.AddDiscordClient();
            services.AddClientTokenProviders();

            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy());

            /*
			services.AddHealthChecks()
				.AddNpgSql(Configuration.GetConnectionString("DefaultConnection"),
						name: "PostgreSQL",
						failureStatus: HealthStatus.Unhealthy)
				.AddRedis(Configuration["Redis:ConnectionString"],
						name: "Redis",
						failureStatus: HealthStatus.Unhealthy)
				.AddDiskStorageHealthCheck(options =>
						options.AddDrive(Path.GetPathRoot(AppContext.BaseDirectory),
						minimumFreeMegabytes: 1000),
                        name: "Disk Storage",
						failureStatus: HealthStatus.Degraded);

			services.AddHealthChecksUI(setup =>
			{
				setup.AddHealthCheckEndpoint("Basic Health Check", "https://localhost:5001/health");
				setup.SetEvaluationTimeInSeconds(60);
			}).AddInMemoryStorage();
			*/
        }

        /// <summary>
        /// Configures the HTTP request pipeline.
        /// </summary>
        /// <param name="app">The application builder to configure the middleware pipeline.</param>
        /// <param name="env">The hosting environment information.</param>
        /// <param name="localizationOptions">The request localization options.</param>
        /// <param name="loggerFactory">The logger factory for creating loggers.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IOptions<RequestLocalizationOptions> localizationOptions, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment() || env.IsStaging())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseRequestLoggingMiddleware();
            }
            else
            {
                app.UseExceptionHandler(new ExceptionHandlerOptions
                {
                    AllowStatusCode404Response = true,
                    StatusCodeSelector = ex => ex is TimeoutException
                        ? StatusCodes.Status503ServiceUnavailable
                        : StatusCodes.Status500InternalServerError
                });
                app.UseHsts(hsts => hsts.MaxAge(365));
            }

            app.UseCsp(opts => opts
                .DefaultSources(s => s.Self())
                .ScriptSources(s => s.Self().UnsafeInline().UnsafeEval()
                    .CustomSources("https://cdnjs.cloudflare.com"))
                .StyleSources(s => s.Self().UnsafeInline()
                    .CustomSources("https://fonts.googleapis.com"))
                .FrameAncestors(s => s.None())
            );

            app.UseXContentTypeOptions();
            app.UseReferrerPolicy(opts => opts.NoReferrer());

            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
                },
                DefaultContentType = "application/octet-stream",
                ContentTypeProvider = new FileExtensionContentTypeProvider
                {
                    Mappings =
                    {
                        [".js"] = "application/javascript",
                        [".mjs"] = "application/javascript",
                        [".json"] = "application/json",
                        [".wasm"] = "application/wasm",
                        [".css"] = "text/css",
                        [".html"] = "text/html",
                        [".htm"] = "text/html",
                        [".svg"] = "image/svg+xml",
                        [".png"] = "image/png",
                        [".jpg"] = "image/jpeg",
                        [".jpeg"] = "image/jpeg",
                        [".gif"] = "image/gif",
                        [".webp"] = "image/webp",
                        [".ico"] = "image/x-icon",
                        [".ttf"] = "font/ttf",
                        [".otf"] = "font/otf",
                        [".woff"] = "font/woff",
                        [".woff2"] = "font/woff2",
                        [".eot"] = "application/vnd.ms-fontobject",
                        [".mp4"] = "video/mp4",
                        [".webm"] = "video/webm",
                        [".mp3"] = "audio/mpeg",
                        [".ogg"] = "audio/ogg",
                        [".txt"] = "text/plain",
                        [".xml"] = "application/xml",
                        [".map"] = "application/json"
                    }
                }
            });

            app.UseXfo(xfo => xfo.Deny());
            app.UseRedirectValidation();

            app.UseRouting();
            app.UseCookiePolicy();

            app.UseCors();

            app.UseRequestLocalization(localizationOptions.Value);

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseWebSockets();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
                {
                    Predicate = check => check.Name == "self"
                });

                endpoints.MapControllers()
                    .RequireAuthorization();

                endpoints.MapHub<EventHub>("/eventHub")
                    .RequireAuthorization();
            });
        }
    }
}
