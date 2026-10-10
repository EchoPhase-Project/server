// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Runners.Blocks.Triggers;
using EchoPhase.Services.Blocks;

namespace EchoPhase.Services.Workflows
{
    /// <summary>
    /// Starts scheduled runs: every <see cref="Interval"/>, each due schedule
    /// of an active workflow starts one run (input: { scheduledAt }) and moves
    /// to its next time after now. Missed times (downtime) are not caught up:
    /// one run, then the schedule carries on. Single instance, like the queue.
    /// </summary>
    public class WorkflowScheduler : BackgroundService
    {
        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
        private const int BatchSize = 50;

        private readonly IServiceScopeFactory _scopes;
        private readonly TimeProvider _time;
        private readonly ILogger<WorkflowScheduler> _logger;

        public WorkflowScheduler(IServiceScopeFactory scopes, TimeProvider time, ILogger<WorkflowScheduler> logger)
        {
            _scopes = scopes;
            _time = time;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval, _time);
            do
            {
                try
                {
                    await TickAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Workflow schedule tick failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>Starts everything due now; returns how many runs it started.</summary>
        public async Task<int> TickAsync(CancellationToken ct)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var workflows = scope.ServiceProvider.GetRequiredService<WorkflowRepository>();
            var runs = scope.ServiceProvider.GetRequiredService<BlockRunService>();

            var now = _time.GetUtcNow();
            var due = await workflows.DueSchedulesAsync(now.UtcDateTime, BatchSize, ct);
            int started = 0;

            foreach (var schedule in due)
            {
                var workflow = schedule.Workflow!;
                var input = new Dictionary<string, object?>
                {
                    ["scheduledAt"] = new DateTimeOffset(DateTime.SpecifyKind(schedule.NextRunAt, DateTimeKind.Utc)).ToString("O")
                };

                var result = await runs.StartAsync(workflow.UserId, null, new BlockRunStart(
                    WorkflowService.Parse(workflow), [schedule.BlockId], input, workflow.Id, BlockRunTriggers.Schedule), ct);

                if (result.Successful)
                    started++;
                else
                    // E.g. the owner has too many runs: this time is skipped, not retried in a loop.
                    _logger.LogWarning("Scheduled run of workflow {WorkflowId} block {BlockId} not started: {Error}",
                        workflow.Id, schedule.BlockId, result.Error.Value);

                if (CronSchedule.Next(schedule.Cron, schedule.TimeZone, now) is { } next)
                    schedule.NextRunAt = next.UtcDateTime;
                else
                    workflows.RemoveSchedule(schedule);
            }

            if (due.Count > 0)
                await workflows.SaveAsync(ct);
            return started;
        }
    }
}
