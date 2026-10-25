// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EchoPhase.WebSockets.Cluster
{
    /// <summary>
    /// Joins this replica to the others: receives deliveries (retrying until
    /// the backplane answers) and keeps saying it is alive.
    /// </summary>
    public class GatewayClusterService : BackgroundService
    {
        private readonly IGatewayBackplane _backplane;
        private readonly GatewayDispatcher _dispatcher;
        private readonly TimeProvider _time;
        private readonly ILogger<GatewayClusterService> _logger;

        public GatewayClusterService(IGatewayBackplane backplane, GatewayDispatcher dispatcher, TimeProvider time,
            ILogger<GatewayClusterService> logger)
        {
            _backplane = backplane;
            _dispatcher = dispatcher;
            _time = time;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _backplane.SubscribeAsync(_dispatcher.DeliverAsync, stoppingToken);
                    break;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogWarning(e, "Gateway backplane not reachable; retrying");
                    await Task.Delay(TimeSpan.FromSeconds(5), _time, stoppingToken);
                }
            }

            using var timer = new PeriodicTimer(RedisGatewayBackplane.HeartbeatInterval, _time);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _backplane.HeartbeatAsync(stoppingToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogWarning(e, "Gateway heartbeat failed");
                }
            }
        }
    }
}
