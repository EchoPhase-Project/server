using System.Text;
using System.Globalization;
using System.Text.Json;

using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using HealthChecks.System; 
using HealthChecks.UI.Client;
using HealthChecks.NpgSql;

using ParkSquare.AspNetCore.Sitemap;
using Swashbuckle.AspNetCore.Swagger;

using Grpc.AspNetCore;
using Grpc.AspNetCore.Web;

using EchoPhase.DAL.Postgres;
using EchoPhase.Hubs;
using EchoPhase.Hubs.Managers;
using EchoPhase.Roles;
using EchoPhase.Models;
using EchoPhase.Helpers;
using EchoPhase.Services;
using EchoPhase.Services.Security;
using EchoPhase.Services.Internal;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Middlewares;
using EchoPhase.Repositories;
using EchoPhase.Processors.Handlers;
using EchoPhase.RouteConstraints;

namespace EchoPhase
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        
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

			TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
			services.AddSingleton(timeZone);


			services.AddSwaggerGen(o =>
			{
				o.SwaggerDoc("v1", new OpenApiInfo
				{
					Version = "v1",
					Title = "EchoPhase API",
					Description = "API v1",
					TermsOfService = new Uri("https://example.com/terms"),
					Contact = new OpenApiContact
					{
						Name = "Labo Daniil",
						Email = "extremal.user@gmail.com",
						Url = new Uri("https://github.com/CodeHeister"),
					},
					License = new OpenApiLicense
					{
						Name = "Use under LICX",
						Url = new Uri("https://example.com/license"),
					}
				});
			});

			services.AddHttpContextAccessor();

			services.AddRedisCache(Configuration.GetSection("Redis"));
			//services.AddTwitchClient(Configuration.GetSection("Twitch"));
			services.AddDiscordClient(Configuration.GetSection("Discord"));
			services.AddAuthentication(Configuration.GetSection("Jwt"));

			services.AddAuthorization(options =>
			{
				options.AddPolicy("AdminOnly", policy =>
				{
					policy.RequireRole("Admin");
				});

				options.AddPolicy("AdminOrDevOnly", policy =>
				{
					policy.RequireRole("Admin", "Dev");
				});

				options.AddPolicy("StaffOrHigherOnly", policy =>
				{
					policy.RequireRole("Admin", "Dev", "Staff");
				});

				options.AddPolicy("APIAccess", policy =>
				{
					policy.RequireRole("Admin", "Dev", "APIDev");
				});

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

			services.AddViews();
			services.AddCorsPolicies();
			services.AddAntiforgeryOptions();
			services.AddLocalizationOptions();

			services.AddDbContext<PostgresContext>(options =>
				options
					.UseNpgsql(Configuration.GetConnectionString("DefaultConnection"),
						o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

			services.AddHttpsRedirection(options =>
			{
				options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
				options.HttpsPort = 5001;
			});

            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 102400000;
            });

			services.AddSingleton<IStringLocalizerFactory, ResourceManagerStringLocalizerFactory>();
			services.AddSingleton<IUserConnectionManager, UserConnectionManager>();

			services.AddSingleton<FileHelper>();
			services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

			services.AddSingleton<QrCodeService>();

			services.AddEventService();

			services.AddScoped<UserRepository>();

			services.AddScoped<RoleManager<UserRole>>();
			services.AddScoped<IUserService, UserService>();
			services.AddScoped<IAuthService, AuthService>();
			services.AddScoped<RoleService>();

			services.AddScoped<IAntiforgeryService, AntiforgeryService>();
			services.AddScoped<UserManager<User>, UserManager<User>>();
			services.AddScoped<SignInManager<User>, SignInManager<User>>();

			services.AddHostedService<ShutdownService>();

			services.AddGrpc();

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

			services.AddSingleton<OpCodeHandlerResolver>();
		}

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IOptions<RequestLocalizationOptions> localizationOptions, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

				app.UseSwagger();
				app.UseSwaggerUI(o =>
				{
					o.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
					o.RoutePrefix = string.Empty;
				});
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

            app.UseRequestLoggingMiddleware();

            // app.UseHttpsRedirection();

			app.UseDefaultFiles();
			app.UseStaticFiles();

            app.UseStaticFiles(new StaticFileOptions
			{
				OnPrepareResponse = ctx =>
				{
					ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
				},
				ServeUnknownFileTypes = true,
				DefaultContentType = "application/octet-stream",
				ContentTypeProvider = new FileExtensionContentTypeProvider
				{
					Mappings = 
					{ 
						[".js"] = "application/javascript",
						[".json"] = "application/json",
						[".wasm"] = "application/wasm"
					}
				}
			});

			app.UseStaticFiles(new StaticFileOptions
			{
				FileProvider = new PhysicalFileProvider(
					Path.Combine(env.ContentRootPath, "Frontend", "dist")),
				RequestPath = "/app"
			});

            app.UseRedirectionMiddleware();

            app.UseRouting();
			app.UseCookiePolicy();

			app.UseGrpcWeb();
			app.UseCors();

            app.UseRequestLocalization(localizationOptions.Value);

			app.UseAntiforgery();

            app.UseAuthentication();
            app.UseAuthorization();

			app.UseWebSockets();

            app.UseEndpoints(endpoints =>
            {
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

				/*
				endpoints.MapGrpcService<GrpcDataService>()
					.EnableGrpcWeb()
					.RequireCors("AllowAllGrpc")
					.RequireAuthorization();
					*/

				endpoints.Map("/app/{*path:nonfile}", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.SendFileAsync(Path.Combine(env.ContentRootPath, "Frontend", "dist", "index.html"));
                });
					//.RequireAuthorization("AdminOrDevOnly");
			});

            app.UseSitemap();
        }
    }
}
