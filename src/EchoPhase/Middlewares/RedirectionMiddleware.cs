// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Middlewares
{
    public class RedirectionMiddleware
    {
        public static readonly Dictionary<int, string> StatusCodeToPath = new Dictionary<int, string>
        {
            { 404, "/error/not-found" },
            { 403, "/error/access-denied" },
            { 500, "/error/server-error" },
            { 401, "/auth/login" }
        };

        private static IEnumerable<string> staticFileExtensions = new[]
        {
            ".css",
            ".js",
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".svg",
            ".ico",
            ".woff",
            ".woff2",
            ".ttf",
            ".otf",
            ".eot"
        };

        private readonly RequestDelegate _next;

        public RedirectionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            await _next(context);

            if (context.Response.HasStarted)
                return;

            var requestPath = context.Request.Path.Value ?? string.Empty;
            bool isStaticFile = staticFileExtensions.Any(ext => requestPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

            if (!isStaticFile && StatusCodeToPath.TryGetValue(context.Response.StatusCode, out string? redirectPath))
                context.Response.Redirect(redirectPath);
        }
    }

    public static class NotFoundMiddlewareExtensions
    {
        public static IApplicationBuilder UseRedirectionMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RedirectionMiddleware>();
        }
    }
}
