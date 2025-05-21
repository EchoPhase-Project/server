using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace EchoPhase.Middlewares
{
	public class RequestLoggingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<RequestLoggingMiddleware> _logger;

		public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
		{
			_next = next;
			_logger = logger;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var remoteIpAddress = context.Connection.RemoteIpAddress?.ToString();

			_logger.LogInformation("Request info: {Time} {IP} {Path}", 
					DateTime.UtcNow.ToLocalTime().ToString("HH:mm:ss"),
					remoteIpAddress,
					context.Request.Path);

			// Call the next delegate/middleware in the pipeline
			await _next(context);
		}
	}

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLoggingMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
