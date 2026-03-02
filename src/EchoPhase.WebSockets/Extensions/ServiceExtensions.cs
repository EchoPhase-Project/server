using EchoPhase.WebSockets.Processors;
using EchoPhase.WebSockets.Processors.Handlers;
using Microsoft.Extensions.DependencyInjection;

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
