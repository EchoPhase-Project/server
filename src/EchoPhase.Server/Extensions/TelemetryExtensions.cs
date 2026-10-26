// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using EchoPhase.Server.Services.Blocks;
using EchoPhase.Server.Services.Diagnostics;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EchoPhase.Server.Extensions
{
    public static class TelemetryExtensions
    {
        public const string ServiceName = "echophase-server";

        /// <summary>The standard variables that say where OTLP goes.</summary>
        private static readonly string[] Endpoints =
        [
            "OTEL_EXPORTER_OTLP_ENDPOINT",
            "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
            "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
        ];

        /// <summary>
        /// Traces and metrics over OTLP, configured by the standard OTEL_*
        /// variables (endpoints, protocol, headers). Off unless an endpoint is
        /// set: nothing is collected for nobody, and nothing tries localhost.
        /// Traces: requests (not health checks), outgoing HTTP, Npgsql and
        /// block runs. Metrics: the same plus the .NET runtime.
        /// </summary>
        public static IServiceCollection AddTelemetry(this IServiceCollection services, IConfiguration configuration)
        {
            if (!Endpoints.Any(name => !string.IsNullOrWhiteSpace(configuration[name])))
                return services;

            var version = ServerVersion.Current;

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(ServiceName, serviceVersion: version))
                .WithTracing(tracing => tracing
                    .SetSampler(new ParentBasedSampler(new NoClientRootsSampler()))
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = http => !http.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation()
                    .AddNpgsql()
                    .AddSource(BlockTelemetry.Name))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Npgsql")
                    .AddMeter(BlockTelemetry.Name))
                .UseOtlpExporter();

            return services;
        }
    }

    /// <summary>
    /// Keeps traces that start with a request or a block run (and all they
    /// call); drops those that start with a client call: queries and HTTP
    /// calls of background loops (the scheduler queries every 30 s), each of
    /// which would otherwise be a trace of its own.
    /// </summary>
    public sealed class NoClientRootsSampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            => new(samplingParameters.Kind == ActivityKind.Client
                ? SamplingDecision.Drop
                : SamplingDecision.RecordAndSample);
    }
}
