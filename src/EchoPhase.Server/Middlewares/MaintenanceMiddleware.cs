// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Redis.Maintenance;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Server.Middlewares
{
    /// <summary>
    /// While maintenance is on, the API answers 503 with Retry-After and the
    /// usual error body (code "Maintenance"), so the webapp can show its
    /// maintenance screen and API clients know to come back. Those who may
    /// switch it (maintenance:update) keep using the API to check their work;
    /// signing in, the status and the switch itself stay reachable for that.
    /// Health checks are outside /api and never affected.
    /// </summary>
    public class MaintenanceMiddleware
    {
        public const string ErrorCode = "Maintenance";
        public const string DefaultMessage = "EchoPhase is under maintenance. Please try again later.";

        private static readonly PathString[] AlwaysOpen = ["/api/v1/status", "/api/v1/auth", "/api/v1/maintenance"];

        private static readonly string BypassPolicy =
            RequirePermissionAttribute.PolicyName(Resources.Maintenance, Permissions.Update);

        private readonly RequestDelegate _next;

        public MaintenanceMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context, MaintenanceSwitch maintenance, IAuthorizationService authorization, TimeProvider time)
        {
            var path = context.Request.Path;
            if (!path.StartsWithSegments("/api") || AlwaysOpen.Any(open => path.StartsWithSegments(open)))
            {
                await _next(context);
                return;
            }

            var state = await maintenance.CurrentAsync(context.RequestAborted);
            if (state is null || await MayBypassAsync(context, authorization))
            {
                await _next(context);
                return;
            }

            var retryAfter = state.RetryAfter(time.GetUtcNow());
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = ((long)retryAfter.TotalSeconds).ToString();
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new ErrorBody(ErrorCode, state.Message ?? DefaultMessage), context.RequestAborted);
        }

        private static async Task<bool> MayBypassAsync(HttpContext context, IAuthorizationService authorization)
            => context.User.Identity?.IsAuthenticated == true
               && (await authorization.AuthorizeAsync(context.User, BypassPolicy)).Succeeded;
    }

    public static class MaintenanceMiddlewareExtensions
    {
        public static IApplicationBuilder UseMaintenance(this IApplicationBuilder builder)
            => builder.UseMiddleware<MaintenanceMiddleware>();
    }
}
