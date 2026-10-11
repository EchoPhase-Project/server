// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EchoPhase.Server.Services.Health
{
    /// <summary>
    /// Readiness: unhealthy until warm-up has finished, so the pod receives no
    /// traffic while its first request would still pay every cold start.
    /// </summary>
    public sealed class WarmupHealthCheck : IHealthCheck
    {
        public const string Tag = "ready";

        private readonly WarmupState _state;

        public WarmupHealthCheck(WarmupState state)
        {
            _state = state;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
            => Task.FromResult(_state.IsReady
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Warming up."));
    }
}
