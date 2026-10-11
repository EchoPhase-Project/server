// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Net.Http.Headers;
using EchoPhase.Clients.Discord;
using EchoPhase.Clients.Discord.WebSocket;
using EchoPhase.Clients.Twitch;
using EchoPhase.Clients.Twitch.Auth;
using EchoPhase.Connections;
using EchoPhase.Connections.Extensions;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Identity;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Server.Interfaces.Services;
using EchoPhase.Server.Json;
using EchoPhase.Server.Services.Events;
using EchoPhase.WebHooks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.Extensions.Http;

namespace EchoPhase.Server.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddTwitchClient(this IServiceCollection services)
        {
            services.AddHttpClient<ITwitchAuthClient, TwitchAuthClient>("TwitchAuth", (serviceProvider, client) =>
                    {
                        client.BaseAddress = new Uri("https://id.twitch.tv/oauth2/");

                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            services.AddHttpClient<ITwitchClient, TwitchClient>("Twitch", (serviceProvider, client) =>
                    {
                        client.BaseAddress = new Uri("https://api.twitch.tv/helix/");

                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddConnectionAuth(ConnectionProviders.Twitch)
                .AddPolicyHandler(request =>
                    request.Method == HttpMethod.Get ?
                        GetRetryPolicy() :
                        GetNoOpPolicy()
                );

            return services;
        }

        public static IServiceCollection AddDiscordClient(this IServiceCollection services)
        {
            services.AddSingleton<DiscordWebSocketManager>();

            services.AddHttpClient<IDiscordClient, DiscordClient>("Discord", (serviceProvider, client) =>
                    {
                        client.BaseAddress = new Uri("https://discord.com/api/v10/");

                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddConnectionAuth(ConnectionProviders.Discord)
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


        public static IServiceCollection AddApiControllers(this IServiceCollection services)
        {
            // JSON API controllers only: no views, Razor or Razor Pages.
            services.AddControllers()
                // Request times must carry Z or an offset (see StrictTime).
                .AddJsonOptions(o =>
                {
                    o.JsonSerializerOptions.Converters.Add(new StrictDateTimeOffsetConverter());
                    o.JsonSerializerOptions.Converters.Add(new StrictUtcDateTimeConverter());
                })
                // Validation failures use the API's error body, with the field each
                // message is about, instead of ProblemDetails.
                .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory =
                    context => new BadRequestObjectResult(ErrorBody.From(
                        context.ModelState, context.ActionDescriptor.Parameters.Select(p => p.Name))));

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
            services.AddScoped<IUserPermissionService, UserPermissionService>();

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
