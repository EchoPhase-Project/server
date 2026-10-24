// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using EchoPhase.Configuration.Extensions;
using EchoPhase.Connections.Extensions;
using EchoPhase.Connections.Services;
using EchoPhase.DAL.Postgres.Extensions;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Redis.Extensions;
using EchoPhase.DAL.Scylla.Extensions;
using EchoPhase.Identity;
using EchoPhase.Profilers.Extensions;
using EchoPhase.Projection.Extensions;
using EchoPhase.QRCodes.Extensions;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Observers;
using EchoPhase.Runners.Blocks.Runs;
using EchoPhase.Runners.Extensions;
using EchoPhase.Security.Antiforgery.Extensions;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authentication.Impersonation;
using EchoPhase.Security.Authorization.Extensions;
using EchoPhase.Security.BitMasks.Extensions;
using EchoPhase.Security.Cryptography.Extensions;
using EchoPhase.Security.Hashers.Extensions;
using EchoPhase.Server.Extensions;
using EchoPhase.Server.Middlewares;
using EchoPhase.Server.OpenApi;
using EchoPhase.Server.RouteConstraints;
using EchoPhase.Server.Services.Blocks;
using EchoPhase.Server.Services.Connections;
using EchoPhase.Server.Services.Health;
using EchoPhase.Server.Services.Impersonation;
using EchoPhase.Server.Services.Permissions;
using EchoPhase.Server.Services.Realtime;
using EchoPhase.Server.Services.Workflows;
using EchoPhase.WebSockets.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace EchoPhase.Server
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
                // Scopes carry the caller (PrincipalLoggingMiddleware) and the request id.
                builder.AddJsonConsole(o =>
                {
                    o.IncludeScopes = true;
                    o.UseUtcTimestamp = true;
                    o.TimestampFormat = "O";
                    // Logs are never embedded in HTML; keep '+' and non-ASCII readable.
                    o.JsonWriterOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                });
                builder.AddDebug();
                builder.AddFile(
                    "Logs/logs-{Date}.txt",
                    outputTemplate: "{Timestamp:o} {RequestId,13} [{Level:u3}] {Principal} {Message} ({EventId:x8}){NewLine}{Exception}");
            });

            services.Configure<RouteOptions>(options =>
            {
                options.ConstraintMap.Add("ulong", typeof(ULongRouteConstraint));
                options.ConstraintMap.Add("username", typeof(UsernameRouteConstraint));
            });

            // The clock: inject TimeProvider instead of reading DateTime.UtcNow,
            // so tests can substitute FakeTimeProvider.
            services.AddSingleton(TimeProvider.System);

            services.AddSwaggerGen(o =>
            {
                // Honour C# nullability: non-nullable members are required and
                // not null, so generated client types are not all optional.
                o.SupportNonNullableReferenceTypes();
                o.NonNullableReferenceTypesAsRequired();
                // Enums that grow (block types): a new value is not a breaking change.
                o.SchemaFilter<ExtensibleEnumSchemaFilter>();

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                // Generated in Debug only (not shipped); the document still builds without it.
                if (File.Exists(xmlPath))
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

            services.AddConfiguredForwardedHeaders();
            services.AddConfiguredRateLimiting();

            services.AddApiControllers();
            services.AddCorsPolicies();
            services.AddConfiguredAntiforgery();
            services.AddLocalizationOptions();
            services.AddConfigurations();

            services.AddSingleton<IStringLocalizerFactory, ResourceManagerStringLocalizerFactory>();
            services.AddHostedService<RealtimeCredentialMonitor>();
            services.AddHostedService<PermissionGrantExpiryWorker>();

            services.AddProjection();

            services.AddCryptography();
            services.AddPasswordHasher();
            services.AddBitMasks();

            services.AddQRCodes();

            services.AddScoped<IUserService, UserService>();

            services.AddScoped<UserManager<User>, UserManager<User>>();

            services.AddProfiler();

            services.AddRunners();
            services.AddSingleton<BlockRunRegistry>();
            services.AddSingleton<BlockRunQueue>();
            services.AddScoped<BlockRunService>();
            services.AddScoped<BlockRunEventsNotifier>();
            services.AddScoped<WorkflowService>();
            services.AddScoped<IWorkflowSource, WorkflowProgramSource>();
            services.AddHostedService<WorkflowScheduler>();
            services.AddSingleton<DiscordTriggerService>();
            services.AddHostedService(sp => sp.GetRequiredService<DiscordTriggerService>());
            services.AddHostedService<DiscordGatewayErrorHandler>();
            services.AddScoped<IBlockRunObserver, BlockRunProgressObserver>();
            // One recorder per run, read by the worker for the run's execution data.
            services.AddScoped<ExecutionRecorder>();
            services.AddScoped<IBlockRunObserver>(sp => sp.GetRequiredService<ExecutionRecorder>());
            services.AddHostedService<BlockRunWorker>();

            services.AddWebSocket();
            services.AddEventService();

            services.AddPostgres();
            services.AddScylla();
            services.AddRedisCache();

            services.AddRoles();

            services.AddAuthentications();
            services.AddAuthorizations();
            services.AddProblemDetails();

            services.AddDiscordClient();
            services.AddTwitchClient();
            services.AddExternalConnections();
            services.AddScoped<IConnectionEvents, ConnectionEventsNotifier>();
            services.AddScoped<IImpersonationEvents, ImpersonationEventsNotifier>();

            services.AddSingleton<WarmupState>();
            services.AddHostedService<WarmupService>();
            services.AddHealthChecks()
                // Liveness: the process answers. Never depends on dependencies or warm-up,
                // or a slow start would get the pod restarted.
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddCheck<WarmupHealthCheck>("warmup", tags: [WarmupHealthCheck.Tag]);

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
            // First: everything after it (logging, HTTPS checks, rate limits) must
            // see the client's real IP and scheme, not the proxy's.
            app.UseForwardedHeaders();

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

            app.UseXfo(xfo => xfo.Deny());
            app.UseRedirectValidation();

            app.UseRouting();
            app.UseCookiePolicy();

            app.UseCors();

            app.UseRequestLocalization(localizationOptions.Value);

            app.UseAuthentication();
            // Tag the rest of the request's logs with the caller (user, API client, actor).
            app.UsePrincipalLogging();
            // Before rate limits and authorization: during maintenance the API
            // answers 503 at once (its switchers excepted).
            app.UseMaintenance();
            // After authentication so limits can be per user / API client.
            app.UseRateLimiter();
            app.UseAuthorization();

            app.UseWebSockets();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
                {
                    Predicate = check => check.Name == "self"
                }).DisableRateLimiting();

                // Readiness: traffic only once warm-up has paid the cold start.
                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains(WarmupHealthCheck.Tag)
                }).DisableRateLimiting();

                endpoints.MapControllers()
                    .RequireAuthorization();
            });
        }
    }
}
