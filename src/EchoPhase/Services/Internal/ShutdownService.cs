using EchoPhase.Hubs;
using EchoPhase.Services.WebSockets;
using Microsoft.AspNetCore.SignalR;

namespace EchoPhase.Services.Internal
{
    public class ShutdownService : IHostedService
    {
        private readonly WebSocketConnectionManager _webSocketConnectionManager;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly IHubContext<EventHub> _hubContext;
        public static bool IsShuttingDown = false;

        public ShutdownService(
                WebSocketConnectionManager webSocketConnectionManager,
                IHostApplicationLifetime appLifetime,
                IHubContext<EventHub> hubContext)
        {
            _webSocketConnectionManager = webSocketConnectionManager;
            _appLifetime = appLifetime;
            _hubContext = hubContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStopping.Register(OnApplicationStopping);
            _appLifetime.ApplicationStopped.Register(OnApplicationStopped);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void OnApplicationStopping()
        {
            Console.WriteLine("Application is shutting down. Notifying clients...");
            IsShuttingDown = true;
            Task.Run(async () =>
            {
                await _webSocketConnectionManager.CloseConnectionsAsync();
            }).Wait();
        }

        private void OnApplicationStopped()
        {
            Task.Run(() =>
            {
                _webSocketConnectionManager.AbortConnections();
            }).Wait();
        }
    }
}
