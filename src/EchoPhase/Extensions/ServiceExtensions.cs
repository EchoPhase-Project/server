// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Net.Http.Headers;
using EchoPhase.Clients;
using EchoPhase.Clients.Discord;
using EchoPhase.Clients.Providers;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Identity;
using EchoPhase.Interfaces.Services;
using EchoPhase.Services.Events;
using EchoPhase.WebHooks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Polly;
using Polly.Extensions.Http;

namespace EchoPhase.Extensions
{
    public static class ServiceExtensions
    {
        /*
        public static IServiceCollection AddTwitchClient(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.AddHttpClient<TwitchClient>("Twitch", (serviceProvider, client) =>
            {
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

        public static IServiceCollection AddDiscordClient(this IServiceCollection services)
        {
            services.AddSingleton<IClientSecretVault, ClientSecretVault>();
            services.AddScoped<ClientAccessProvider>();
            services.AddScoped<IExternalTokenService, ExternalTokenService>();
            services.AddScoped<IClientTokenProviderRegistry>(
                sp => new ClientTokenProviderRegistry(sp));
            services.AddHttpClient<IDiscordClient, DiscordClient>("Discord", (serviceProvider, client) =>
                    {
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

            services.AddMvc();

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

        public static IServiceCollection AddRoles(this IServiceCollection services)
        {
            services.AddScoped<RoleManager<UserRole>>();
            services.AddScoped<IRoleService, RoleService>();

            return services;
        }

        public static IServiceCollection AddEventService(this IServiceCollection services)
        {
            services.AddScoped<WebHookService>();
            services.AddScoped<IEventService, EventService>();

            return services;
        }
    }
}
