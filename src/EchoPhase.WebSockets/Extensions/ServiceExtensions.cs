using Microsoft.Extensions.DependencyInjection;
using EchoPhase.WebSockets.Processors;
using EchoPhase.WebSockets.Processors.Handlers;

namespace EchoPhase.WebSockets.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddWebSocket(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddSingleton<WebSocketConnectionManager>();
            services.AddScoped<WebSocketService>();
            services.AddScoped<WebSocketProcessor>();
            services.AddSingleton<OpCodeHandlerResolver>();

            return services;
        }
    }
}
