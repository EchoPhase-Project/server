// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Middlewares
{
    /// <summary>
    /// Middleware that serves pre-compressed .gz static files
    /// when the client supports gzip encoding.
    /// </summary>
    public class GzipStaticFileMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _env;

        /// <summary>
        /// Initializes a new instance of the <see cref="GzipStaticFileMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="env">The hosting environment to access web root path.</param>
        public GzipStaticFileMiddleware(RequestDelegate next, IWebHostEnvironment env)
        {
            _next = next;
            _env = env;
        }

        /// <summary>
        /// Invokes the middleware to check if the client supports gzip encoding.
        /// If yes, attempts to serve the pre-compressed .gz file from wwwroot.
        /// If no .gz file is found or gzip is not supported, passes control to next middleware.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <returns>A task that represents the completion of request processing.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            // Read Accept-Encoding header to check for gzip support
            var acceptEncoding = context.Request.Headers["Accept-Encoding"].ToString();

            if (!acceptEncoding.Contains("gzip"))
            {
                await _next(context);
                return;
            }

            var originalPath = context.Request.Path.Value;
            if (originalPath == null)
            {
                await _next(context);
                return;
            }

            var gzFilePath = Path.Combine(_env.WebRootPath, originalPath.TrimStart('/')) + ".gz";

            if (File.Exists(gzFilePath))
            {
                context.Response.Headers["Content-Encoding"] = "gzip";

                var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                if (!contentTypeProvider.TryGetContentType(originalPath, out var contentType))
                {
                    contentType = "application/octet-stream";
                }

                context.Response.ContentType = contentType;

                await context.Response.SendFileAsync(gzFilePath);
                return;
            }

            await _next(context);
        }
    }
}
