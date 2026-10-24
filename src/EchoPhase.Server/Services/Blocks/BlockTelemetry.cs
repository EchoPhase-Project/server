// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using EchoPhase.DAL.Scylla.Entities;

namespace EchoPhase.Server.Services.Blocks
{
    /// <summary>
    /// Block runs for OpenTelemetry: a span per run (its HTTP calls and
    /// queries nest under it) and metrics by status and trigger. Collected
    /// only when telemetry is exported (see TelemetryExtensions).
    /// </summary>
    public static class BlockTelemetry
    {
        public const string Name = "EchoPhase.Blocks";

        public static readonly ActivitySource Source = new(Name);

        private static readonly Meter Meter = new(Name);

        private static readonly Counter<long> Runs = Meter.CreateCounter<long>(
            "echophase.block_runs", unit: "{run}", description: "Finished block runs.");

        private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
            "echophase.block_run.duration", unit: "s", description: "How long block runs ran.");

        private static readonly Histogram<long> Steps = Meter.CreateHistogram<long>(
            "echophase.block_run.steps", unit: "{step}", description: "Blocks a run executed, loop iterations included.");

        public static Activity? Start(BlockRun run)
        {
            var activity = Source.StartActivity("block run");
            activity?.SetTag("echophase.run.id", run.RunId);
            activity?.SetTag("echophase.run.trigger", run.Trigger);
            if (run.WorkflowId is { } workflowId)
                activity?.SetTag("echophase.workflow.id", workflowId);
            return activity;
        }

        public static void Finished(Activity? activity, BlockRun run, TimeSpan elapsed, int steps)
        {
            var status = run.Status.ToString();
            activity?.SetTag("echophase.run.status", status);
            activity?.SetTag("echophase.run.steps", steps);
            if (run.Status is not BlockRunStatus.Succeeded)
                activity?.SetStatus(ActivityStatusCode.Error, status);

            var tags = new TagList { { "status", status }, { "trigger", run.Trigger } };
            Runs.Add(1, tags);
            Duration.Record(elapsed.TotalSeconds, tags);
            Steps.Record(steps, tags);
        }
    }
}
