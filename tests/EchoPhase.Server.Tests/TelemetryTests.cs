// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Server.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace EchoPhase.Server.Tests
{
    public class TelemetryTests
    {
        private static ServiceProvider Build(Dictionary<string, string?> settings)
            => new ServiceCollection()
                .AddLogging()
                .AddTelemetry(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
                .BuildServiceProvider();

        [Fact]
        public void OffWithoutAnEndpoint()
        {
            using var services = Build([]);
            Assert.Null(services.GetService<TracerProvider>());
            Assert.Null(services.GetService<MeterProvider>());
        }

        [Fact]
        public void OnWithOne()
        {
            using var services = Build(new()
            {
                ["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"] = "http://tempo:4318/v1/traces",
                ["OTEL_EXPORTER_OTLP_METRICS_ENDPOINT"] = "http://victoria-metrics:8428/opentelemetry/v1/metrics",
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
            });
            Assert.NotNull(services.GetService<TracerProvider>());
            Assert.NotNull(services.GetService<MeterProvider>());
        }
    }
}
