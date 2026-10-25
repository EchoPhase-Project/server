// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EchoPhase.Configuration.Runner;
using EchoPhase.Configuration.Runner.Blocks;
using EchoPhase.DAL.Scylla;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.DAL.Scylla.Linq;
using EchoPhase.DAL.Scylla.Models;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Observers;
using EchoPhase.Runners.Blocks.Runs;
using EchoPhase.Server.Services.Workflows;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Services.Blocks
{
    /// <summary>
    /// Executes queued block runs, <see cref="BlocksOptions.Workers"/> at a time,
    /// each in its own DI scope and under its own timeout. Status changes are
    /// stored and pushed to the owner as events.
    /// </summary>
    public class BlockRunWorker : BackgroundService
    {
        private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly BlockRunQueue _queue;
        private readonly BlockRunRegistry _registry;
        private readonly IServiceScopeFactory _scopes;
        private readonly BlocksOptions _options;
        private readonly ILogger<BlockRunWorker> _logger;
        private readonly TimeProvider _time;

        public BlockRunWorker(
            BlockRunQueue queue,
            BlockRunRegistry registry,
            IServiceScopeFactory scopes,
            IOptions<RunnerOptions> options,
            ILogger<BlockRunWorker> logger,
            TimeProvider time)
        {
            _queue = queue;
            _registry = registry;
            _scopes = scopes;
            _options = options.Value.Blocks;
            _logger = logger;
            _time = time;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RecoverInterruptedAsync(stoppingToken);

            try
            {
                await Parallel.ForEachAsync(
                    _queue.ReadAllAsync(stoppingToken),
                    new ParallelOptions { MaxDegreeOfParallelism = _options.Workers, CancellationToken = stoppingToken },
                    async (job, ct) => await ProcessAsync(job, ct));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown. Runs still queued stay in block_runs_active and are
                // marked interrupted on the next start.
            }
        }

        private async Task ProcessAsync(BlockRunJob job, CancellationToken stoppingToken)
        {
            var run = job.Run;

            using var _ = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["RunId"] = run.RunId,
                ["UserId"] = run.UserId
            });

            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ScyllaContext>();
            var events = scope.ServiceProvider.GetRequiredService<BlockRunEventsNotifier>();
            var runner = scope.ServiceProvider.GetRequiredService<BlocksRunner>();
            var recorder = scope.ServiceProvider.GetRequiredService<ExecutionRecorder>();

            IBlockExecutionContext? context = null;
            var stopwatch = new Stopwatch();
            using var activity = BlockTelemetry.Start(run);

            try
            {
                if (job.Cancelled.IsCancellationRequested)
                {
                    run.Status = BlockRunStatus.Cancelled;
                    return;
                }

                run.Status = BlockRunStatus.Running;
                run.StartedAt = _time.GetUtcNow();
                db.BlockRuns.Add(run, History);
                await db.SaveChangesAsync(stoppingToken);
                await SafeAsync(() => events.StartedAsync(run, stoppingToken), "started event");

                // The program may ask for less time than the server allows, never more.
                var limit = job.Program.Settings.TimeoutSeconds is { } seconds
                    ? TimeSpan.FromSeconds(Math.Min(seconds, _options.Timeout.TotalSeconds))
                    : _options.Timeout;
                using var timeout = new CancellationTokenSource(limit, _time);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken, job.Cancelled, timeout.Token);

                context = new BlockExecutionContext(job.Program, scope.ServiceProvider, run.UserId, linked.Token)
                {
                    Limits = new BlockLimits(_options.MaxSteps, _options.MaxOutputEntries),
                    RunId = run.RunId,
                    WorkflowId = run.WorkflowId,
                    Reply = job.Reply
                };

                stopwatch.Start();
                try
                {
                    await runner.RunAsync(context, job.StartIds, job.Input);
                    run.Status = context.Errors.Count > 0 ? BlockRunStatus.Failed : BlockRunStatus.Succeeded;
                }
                catch (OperationCanceledException) when (linked.IsCancellationRequested)
                {
                    run.Status = job.Cancelled.IsCancellationRequested ? BlockRunStatus.Cancelled
                        : timeout.IsCancellationRequested ? BlockRunStatus.TimedOut
                        : BlockRunStatus.Interrupted;
                }
                finally
                {
                    stopwatch.Stop();
                }
            }
            // Never let one run take the worker down: every failure ends up in the run.
            catch (Exception ex)
            {
                run.Status = stoppingToken.IsCancellationRequested ? BlockRunStatus.Interrupted : BlockRunStatus.Failed;
                if (run.Status == BlockRunStatus.Failed)
                    _logger.LogError(ex, "Block run {RunId} failed unexpectedly.", run.RunId);
            }
            finally
            {
                BlockTelemetry.Finished(activity, run, stopwatch.Elapsed, context?.Steps ?? 0);
                await FinishAsync(db, events, run, context, recorder, job, stopwatch.Elapsed);
                _registry.Release(run.RunId);
                // After the result is stored, so the caller can fetch the run at once.
                job.Reply?.TrySend(DefaultReply(job.Reply.Mode, run, context));
                await StartErrorWorkflowAsync(scope.ServiceProvider, run, context, job.Program.Settings, stoppingToken);
            }
        }

        /// <summary>
        /// Like n8n: a failed or timed-out automatic run (not a manual one or a
        /// retry, not one started by an ErrorTrigger, so error workflows never loop) starts
        /// the workflow's error workflow. Released first, so it counts against
        /// the owner's active runs like any other.
        /// </summary>
        private async Task StartErrorWorkflowAsync(
            IServiceProvider services, BlockRun run, IBlockExecutionContext? context, BlockProgramSettings settings,
            CancellationToken ct)
        {
            if (settings.ErrorWorkflowId is not { } errorWorkflowId
                || run.WorkflowId is null
                || run.Status is not (BlockRunStatus.Failed or BlockRunStatus.TimedOut)
                || run.Trigger is BlockRunTriggers.Manual or BlockRunTriggers.Error or BlockRunTriggers.Retry)
                return;

            try
            {
                var workflows = services.GetRequiredService<WorkflowService>();
                var started = await workflows.StartErrorWorkflowAsync(run, errorWorkflowId, context?.Errors.ToList() ?? [], ct);
                if (!started.Successful)
                    _logger.LogWarning("Error workflow {ErrorWorkflowId} of failed run {RunId} not started: {Error}",
                        errorWorkflowId, run.RunId, started.Error.Value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error workflow {ErrorWorkflowId} of failed run {RunId} could not be started.",
                    errorWorkflowId, run.RunId);
            }
        }

        /// <summary>
        /// Stores the outcome even when the run was cancelled or the host is
        /// stopping: these writes are short and must not be lost.
        /// </summary>
        private async Task FinishAsync(
            ScyllaContext db, BlockRunEventsNotifier events, BlockRun run, IBlockExecutionContext? context,
            ExecutionRecorder recorder, BlockRunJob job, TimeSpan elapsed)
        {
            var settings = job.Program.Settings;
            run.FinishedAt = _time.GetUtcNow();
            run.DurationMs = (long)elapsed.TotalMilliseconds;
            run.Steps = context?.Steps ?? 0;

            // Per-block data as the program asks: always, only for failed runs, or never.
            var keepExecution = settings.SaveExecutionData switch
            {
                ExecutionDataPolicy.All => true,
                ExecutionDataPolicy.Errors => run.Status != BlockRunStatus.Succeeded,
                _ => false
            };
            var (payload, errorCount, tooLarge) = BuildPayload(context, keepExecution ? recorder : null, Start(job));
            run.ErrorCount = errorCount;
            if (tooLarge && run.Status == BlockRunStatus.Succeeded)
                run.Status = BlockRunStatus.Failed;

            try
            {
                db.BlockRuns.Add(run, History);
                db.BlockRunResults.Add(
                    new BlockRunResult { UserId = run.UserId, RunId = run.RunId, Payload = payload },
                    new WriteOptions(Ttl: _options.ResultTtl));
                db.ActiveBlockRuns.Remove(new ActiveBlockRun { RunId = run.RunId, UserId = run.UserId });
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store the outcome of block run {RunId}.", run.RunId);
            }

            _logger.LogInformation("Block run {RunId} finished: {Status} in {DurationMs} ms, {Steps} steps, {ErrorCount} errors.",
                run.RunId, run.Status, run.DurationMs, run.Steps, run.ErrorCount);

            await SafeAsync(() => events.CompletedAsync(run, CancellationToken.None), "completed event");
        }

        /// <summary>
        /// The answer to a waiting webhook caller when no RespondToWebhook gave
        /// one: the last output (OnFinish) or the run's id and status (ByBlock).
        /// A failed run is a bare 500: the caller is a third party, and error
        /// messages may hold what it should not see (URLs, tokens); the owner
        /// reads them in the run.
        /// </summary>
        private static WebhookResponse DefaultReply(WebhookRespondMode mode, BlockRun run, IBlockExecutionContext? context)
        {
            var none = new Dictionary<string, string>();
            if (run.Status != BlockRunStatus.Succeeded)
                return new WebhookResponse(StatusCodes.Status500InternalServerError,
                    new { runId = run.RunId, status = run.Status }, none);

            return mode == WebhookRespondMode.OnFinish
                ? new WebhookResponse(StatusCodes.Status200OK, context?.LastOutput, none)
                : new WebhookResponse(StatusCodes.Status200OK, new { runId = run.RunId, status = run.Status }, none);
        }

        /// <summary>
        /// What started the run, kept with its result for a retry: the program
        /// as it ran, the start blocks and the input. Dropped when it would
        /// take more than half the result's room (a retry then says so).
        /// </summary>
        private object? Start(BlockRunJob job)
        {
            try
            {
                var start = JsonSerializer.SerializeToElement(new
                {
                    program = JsonSerializer.SerializeToElement(job.Program, BlockParamsBinder.Options),
                    startIds = job.StartIds,
                    input = job.Input
                }, PayloadJson);
                return Encoding.UTF8.GetByteCount(start.GetRawText()) <= _options.MaxResultBytes / 2 ? start : null;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                return null;
            }
        }

        private (string Payload, int ErrorCount, bool TooLarge) BuildPayload(
            IBlockExecutionContext? context, ExecutionRecorder? recorder, object? start)
        {
            var errors = context?.Errors.ToList() ?? [];

            string? json = null;
            try
            {
                json = JsonSerializer.Serialize(new
                {
                    output = context?.Output ?? [],
                    variables = context?.Variables ?? new Dictionary<string, object?>(),
                    errors,
                    execution = recorder?.Records,
                    executionDropped = recorder?.Dropped,
                    start
                }, PayloadJson);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                // E.g. a cyclic variable; reported below like an oversized result.
                _logger.LogWarning(ex, "Block run result could not be serialized.");
            }

            if (json is not null && Encoding.UTF8.GetByteCount(json) <= _options.MaxResultBytes)
                return (json, errors.Count, false);

            // Keep the errors, drop the bulk: the run is still explained.
            errors.Add(new BlockError
            {
                Id = -1,
                Type = "ResultTooLarge",
                Message = json is null
                    ? "The result could not be serialized; output and variables were dropped."
                    : $"The result exceeded {_options.MaxResultBytes} bytes; output and variables were dropped."
            });

            var trimmed = JsonSerializer.Serialize(new
            {
                output = Array.Empty<string>(),
                variables = new Dictionary<string, object?>(),
                errors = errors.Take(100),
                start
            }, PayloadJson);

            return (trimmed, errors.Count, true);
        }

        /// <summary>
        /// Runs left in block_runs_active by a previous process never finished:
        /// record them as interrupted. Assumes a single instance, like the queue.
        /// </summary>
        private async Task RecoverInterruptedAsync(CancellationToken ct)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ScyllaContext>();

                foreach (var active in await db.ActiveBlockRuns.ToListAsync(ct))
                {
                    if (_registry.IsActive(active.RunId))
                        continue;

                    var run = await db.BlockRuns
                        .Where(r => r.UserId == active.UserId && r.RunId == active.RunId)
                        .FirstOrDefaultAsync(ct);

                    if (run is not null && !BlockRunStatus.IsFinished(run.Status))
                    {
                        run.Status = BlockRunStatus.Interrupted;
                        run.FinishedAt = _time.GetUtcNow();
                        db.BlockRuns.Add(run, History);
                    }

                    db.ActiveBlockRuns.Remove(active);
                }

                var recovered = await db.SaveChangesAsync(ct);
                if (recovered > 0)
                    _logger.LogWarning("Marked block runs interrupted by the previous shutdown ({Writes} writes).", recovered);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not fatal: the stale rows are retried on the next start.
                _logger.LogError(ex, "Could not recover interrupted block runs.");
            }
        }

        private async Task SafeAsync(Func<Task> action, string what)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Block run {What} could not be delivered.", what);
            }
        }

        private WriteOptions History => new(Ttl: _options.HistoryTtl);
    }
}
