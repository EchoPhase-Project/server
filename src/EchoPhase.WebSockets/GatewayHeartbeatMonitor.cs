// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EchoPhase.WebSockets
{
    /// <summary>
    /// Closes gateway connections that went quiet: one sweep every quarter of
    /// the heartbeat interval for all of them, instead of a timer task per
    /// connection restarted on every ping.
    /// </summary>
    public sealed class GatewayHeartbeatMonitor : BackgroundService
    {
        private readonly WebSocketConnectionManager _connections;
        private readonly WebSocketOptions _settings;
        private readonly TimeProvider _time;
        private readonly ILogger<GatewayHeartbeatMonitor> _logger;

        public GatewayHeartbeatMonitor(
            WebSocketConnectionManager connections,
            IOptions<WebSocketOptions> settings,
            TimeProvider time,
            ILogger<GatewayHeartbeatMonitor> logger)
        {
            _connections = connections;
            _settings = settings.Value;
            _time = time;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_settings.HeartbeatInterval / 4, _time);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _connections.CloseSilentAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Gateway heartbeat sweep failed.");
                }
            }
        }
    }
}
