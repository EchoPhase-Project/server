// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Threading.RateLimiting;
using EchoPhase.Configuration.Network;
using EchoPhase.Configuration.RateLimiting;
using EchoPhase.Server.RateLimiting;
using EchoPhase.Types.Claims;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Extensions
{
    public static class NetworkExtensions
    {
        /// <summary>
        /// Trust <c>X-Forwarded-For</c>/<c>-Proto</c> only from the configured
        /// proxy networks. Pair with <c>app.UseForwardedHeaders()</c> as the very
        /// first middleware.
        /// </summary>
        public static IServiceCollection AddConfiguredForwardedHeaders(this IServiceCollection services)
        {
            services.AddOptions<ForwardedHeadersOptions>()
                .Configure<IOptions<ForwardedHeadersSettings>>((options, settings) =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    options.ForwardLimit = settings.Value.ForwardLimit;

                    // The defaults trust only loopback; replace them with the proxy networks.
                    options.KnownIPNetworks.Clear();
                    options.KnownProxies.Clear();
                    foreach (var network in settings.Value.KnownNetworks)
                        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                });

            return services;
        }

        /// <summary>
        /// A global per-caller limit plus the stricter <see cref="RateLimitPolicies.Auth"/>
        /// policy. Limits are kept in memory per instance: with several replicas
        /// the effective limit multiplies (see ~/echophase-rate-limiting.md).
        /// </summary>
        public static IServiceCollection AddConfiguredRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(_ => { });

            services.AddOptions<RateLimiterOptions>()
                .Configure<IOptions<RateLimitingSettings>>((options, settingsAccessor) =>
                {
                    var settings = settingsAccessor.Value;

                    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    options.OnRejected = (context, _) =>
                    {
                        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                            context.HttpContext.Response.Headers.RetryAfter =
                                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                        return ValueTask.CompletedTask;
                    };

                    options.AddPolicy(RateLimitPolicies.Auth, context => settings.Enabled
                        ? RateLimitPartition.GetSlidingWindowLimiter(
                            "ip:" + ClientIp(context),
                            _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = settings.Auth.PermitLimit,
                                Window = settings.Auth.Window,
                                SegmentsPerWindow = settings.Auth.SegmentsPerWindow,
                                QueueLimit = 0
                            })
                        : RateLimitPartition.GetNoLimiter("off"));

                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                        settings.Enabled
                            ? RateLimitPartition.GetTokenBucketLimiter(
                                CallerKey(context),
                                _ => new TokenBucketRateLimiterOptions
                                {
                                    TokenLimit = settings.Global.TokenLimit,
                                    TokensPerPeriod = settings.Global.TokensPerPeriod,
                                    ReplenishmentPeriod = settings.Global.ReplenishmentPeriod,
                                    QueueLimit = 0,
                                    AutoReplenishment = true
                                })
                            : RateLimitPartition.GetNoLimiter("off"));
                });

            return services;
        }

        private static string ClientIp(HttpContext context)
            => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        /// <summary>
        /// Who is calling: each API client and impersonation gets its own budget,
        /// so a busy bot or a support session does not use up the owner's.
        /// </summary>
        private static string CallerKey(HttpContext context)
        {
            var user = context.User;

            return user.FindFirst(EchoPhaseClaimTypes.ClientId)?.Value is { } clientId ? $"client:{clientId}"
                : user.FindFirst(EchoPhaseClaimTypes.ImpersonationId)?.Value is { } impersonationId ? $"imp:{impersonationId}"
                : user.FindFirst("sub")?.Value is { } userId ? $"user:{userId}"
                : $"ip:{ClientIp(context)}";
        }
    }
}
