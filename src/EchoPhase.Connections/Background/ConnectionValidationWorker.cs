// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Clients;
using EchoPhase.Connections.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EchoPhase.Connections.Background
{
    /// <summary>
    /// Periodically validates OAuth connections and refreshes expiring tokens,
    /// so revoked tokens are noticed before a script needs them.
    /// </summary>
    public class ConnectionValidationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ClientsOptions _options;
        private readonly ILogger<ConnectionValidationWorker> _logger;

        public ConnectionValidationWorker(
            IServiceScopeFactory scopes,
            IOptions<ClientsOptions> options,
            ILogger<ConnectionValidationWorker> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_options.ValidationInterval);

            do
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    await scope.ServiceProvider
                        .GetRequiredService<IConnectionService>()
                        .ValidateAllAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Keep the loop alive; one failed run must not stop validation.
                    _logger.LogError(ex, "Connection validation run failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
