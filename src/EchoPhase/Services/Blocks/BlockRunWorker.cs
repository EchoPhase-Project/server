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
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Observers;
using EchoPhase.Runners.Blocks.Runs;
using Microsoft.Extensions.Options;

namespace EchoPhase.Services.Blocks
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

            using var _ = _logger.BeginScope(new Dictionary<string, object>
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
                    RunId = run.RunId
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
                await FinishAsync(db, events, run, context, recorder, job.Program.Settings, stopwatch.Elapsed);
                _registry.Release(run.RunId);
            }
        }

        /// <summary>
        /// Stores the outcome even when the run was cancelled or the host is
        /// stopping: these writes are short and must not be lost.
        /// </summary>
        private async Task FinishAsync(
            ScyllaContext db, BlockRunEventsNotifier events, BlockRun run, IBlockExecutionContext? context,
            ExecutionRecorder recorder, BlockProgramSettings settings, TimeSpan elapsed)
        {
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
            var (payload, errorCount, tooLarge) = BuildPayload(context, keepExecution ? recorder : null);
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

        private (string Payload, int ErrorCount, bool TooLarge) BuildPayload(IBlockExecutionContext? context, ExecutionRecorder? recorder)
        {
            var errors = context?.Errors.ToList() ?? [];

            string? json = null;
            try
            {
                json = JsonSerializer.Serialize(new
                {
                    output = context?.Output ?? [],
                    variables = context?.Variables ?? new Dictionary<string, object>(),
                    errors,
                    execution = recorder?.Records,
                    executionDropped = recorder?.Dropped
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
                variables = new Dictionary<string, object>(),
                errors = errors.Take(100)
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
