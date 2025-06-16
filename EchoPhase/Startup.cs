using System.Reflection;
using EchoPhase.Extensions;
using EchoPhase.Handlers;
using EchoPhase.Helpers;
using EchoPhase.Hubs;
using EchoPhase.Hubs.Managers;
using EchoPhase.Interfaces;
using EchoPhase.Middlewares;
using EchoPhase.Models;
using EchoPhase.Repositories;
using EchoPhase.Requirements;
using EchoPhase.RouteConstraints;
using EchoPhase.Commands;
using EchoPhase.Services;
using EchoPhase.Services.Grpc;
using EchoPhase.Services.Internal;
using EchoPhase.Services.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using ParkSquare.AspNetCore.Sitemap;

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
            services.AddSitemap();

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
            });

            TimeZoneInfo timeZone = TimeZoneInfo.Local;
            services.AddSingleton(timeZone);

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
            services.AddAntiforgeryOptions();
            services.AddLocalizationOptions();

            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 102400000;
            });

            services.AddSingleton<IStringLocalizerFactory, ResourceManagerStringLocalizerFactory>();
            services.AddSingleton<IUserConnectionManager, UserConnectionManager>();

            services.AddSingleton<FileHelper>();
            services.AddSingleton<ProjectionHelper>();

            services.AddAes(Configuration.GetSection("Aes"));
            services.AddPasswordHasher(Configuration.GetSection("Argon2"));

            services.AddSingleton<QrCodeService>();
            services.AddSingleton<IIntentsService, IntentsService>();
            services.AddSingleton<IPermissionsService, PermissionsService>();

            services.AddScoped<UserRepository>();

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAuthService, AuthService>();

            services.AddScoped<UserManager<User>, UserManager<User>>();
            services.AddScoped<SignInManager<User>, SignInManager<User>>();

            services.AddRunners(Configuration.GetSection("Runners"));

            services.AddHostedService<ShutdownService>();

            services.AddEventService();

            services.AddGrpc();

            services.AddPostgres(Configuration.GetSection("Postgres"));
            services.AddRedisCache(Configuration.GetSection("Redis"));
            services.AddAuthentication(Configuration.GetSection("Authentication"));

            services.AddRoles(Configuration.GetSection("Role"));

            services.AddSingleton<IAuthorizationHandler, PermissionsAuthorizationHandler>();
            services.AddScoped<MigrationCommand>();
            services.AddScoped<AddToRolesCommand>();
            services.AddScoped<CreateUserCommand>();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy =>
                {
                    policy.RequireRole("Admin");
                });

                options.AddPolicy("RequireAdminOrHigher", policy =>
                {
                    policy.RequireRole("Admin", "Dev");
                });

                options.AddPolicy("ApiAccess", policy =>
                {
                    policy.RequireRole("Admin", "Dev");
                });

                options.AddPolicy("TrustedOnly", policy =>
                {
                    policy.RequireRole("Admin", "Dev", "Staff");
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
                    policy.Requirements.Add(new PermissionsRequirement("CanEdit", "CanAdd")));

                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(
                            IdentityConstants.ApplicationScheme,
                            JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            //services.AddTwitchClient(Configuration.GetSection("Twitch"));
            services.AddDiscordClient(Configuration.GetSection("Discord"));

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
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                app.UseSwagger();
                app.UseSwaggerUI(o =>
                {
                    o.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
                    o.RoutePrefix = "swagger";
                });

                app.UseRequestLoggingMiddleware();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.Use(async (context, next) =>
            {
                context.Response.Headers.Append("Content-Security-Policy",
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdnjs.cloudflare.com; " +
                    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net https://unicons.iconscout.com; " +
                    "img-src 'self' blob: https://upload.wikimedia.org ; " +
                    "font-src 'self' https://fonts.gstatic.com https://unicons.iconscout.com; " +
                    "connect-src 'self' https://api.cdnjs.com; " +
                    "upgrade-insecure-requests; " +
                    "block-all-mixed-content");
                await next();
            });

            app.UseMiddleware<GzipStaticFileMiddleware>();

            app.UseDefaultFiles();
            app.UseStaticFiles();

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

            app.UseRouting();
            app.UseCookiePolicy();

            app.UseCors();

            app.UseRequestLocalization(localizationOptions.Value);

            app.UseAntiforgery();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseWebSockets();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<DiscordTokenGrpcServiceImpl>();
                /*
				endpoints.MapHealthChecks("/health", new HealthCheckOptions
				{
					Predicate = _ => true,
					ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
				})
					.RequireCors("AllowLocalhost")
					.AllowAnonymous();

				endpoints.MapHealthChecksUI(options =>
				{
					options.UIPath = "/health-ui";
				})
					.RequireCors("AllowLocalhost")
					.RequireAuthorization("AdminOrDevOnly");
				*/

                endpoints.MapControllers()
                    .RequireAuthorization();

                endpoints.MapHub<EventHub>("/eventHub")
                    .RequireAuthorization();

                endpoints.MapFallback(async context =>
                {
                    if (context.Request.Method != HttpMethods.Get ||
                        Path.HasExtension(context.Request.Path))
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }

                    var filePath = Path.Combine(env.WebRootPath ?? string.Empty, "index.html");

                    if (!File.Exists(filePath))
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsync("index.html file not found.");
                        return;
                    }

                    context.Response.ContentType = "text/html";
                    await context.Response.SendFileAsync(filePath);
                });
            });

            app.UseSitemap();
        }
    }
}
