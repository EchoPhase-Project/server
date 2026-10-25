// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using Cassandra;
using EchoPhase.Configuration.Runner;
using EchoPhase.Configuration.Runner.Blocks;
using EchoPhase.DAL.Scylla;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.DAL.Scylla.Linq;
using EchoPhase.DAL.Scylla.Models;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Runs;
using EchoPhase.Runners.Blocks.Validation;
using EchoPhase.Scripting;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using UUIDNext;

namespace EchoPhase.Server.Services.Blocks
{
    public sealed record BlockRunPage(IReadOnlyList<BlockRun> Items, string? NextCursor);

    public sealed record BlockRunDetails(BlockRun Run, JsonElement? Result);

    /// <summary>
    /// Accepts block programs for background execution and reads their
    /// history. Every read is keyed by the caller's user id, which is the
    /// partition key: another user's run is simply not found.
    /// </summary>
    public class BlockRunService
    {
        private const int MaxPageSize = 100;

        private readonly ScyllaContext _db;
        private readonly BlockRunQueue _queue;
        private readonly BlockRunRegistry _registry;
        private readonly BlocksOptions _options;
        private readonly TimeProvider _time;
        private readonly BlockProgramValidator _validator;
        private readonly IWorkflowSource _workflows;

        public BlockRunService(
            ScyllaContext db,
            BlockRunQueue queue,
            BlockRunRegistry registry,
            IOptions<RunnerOptions> options,
            TimeProvider time,
            BlockProgramValidator validator,
            IWorkflowSource workflows)
        {
            _db = db;
            _queue = queue;
            _registry = registry;
            _options = options.Value.Blocks;
            _time = time;
            _validator = validator;
            _workflows = workflows;
        }

        public async Task<IServiceResult<BlockRun>> StartAsync(
            Guid userId,
            Guid? actorId,
            BlockRunStart start,
            CancellationToken ct = default)
        {
            var issues = _validator.Validate(start.Program, _options.MaxBlocks);
            if (issues.Count > 0)
                return Failure<BlockRun>(BlockRunErrors.Invalid, Describe(issues));

            var startIds = start.StartIds ?? _validator.ManualStartIds(start.Program);
            if (startIds.Count == 0)
                return Failure<BlockRun>(BlockRunErrors.Invalid, "Nothing to start: add a ManualTrigger or set startIds.");
            foreach (var id in startIds)
                if (start.Program.Blocks.All(b => b.Id != id))
                    return Failure<BlockRun>(BlockRunErrors.Invalid, $"Start block {id} does not exist.");

            var run = new BlockRun
            {
                UserId = userId,
                RunId = Uuid.NewSequential(),
                Status = BlockRunStatus.Queued,
                ActorId = actorId,
                WorkflowId = start.WorkflowId,
                ProgramHash = BlockProgramHash.Of(start.Program),
                Trigger = start.Trigger,
                BlockCount = start.Program.Blocks.Count,
                CreatedAt = _time.GetUtcNow()
            };

            if (_registry.TryReserve(userId, run.RunId, _options.MaxActivePerUser) is not { } cancelled)
                return Failure<BlockRun>(BlockRunErrors.TooManyActive,
                    $"At most {_options.MaxActivePerUser} runs may be queued or running at once.");

            try
            {
                // Stored before it is queued, so the worker always finds the row.
                _db.BlockRuns.Add(run, History);
                _db.ActiveBlockRuns.Add(new ActiveBlockRun { RunId = run.RunId, UserId = userId });
                await _db.SaveChangesAsync(ct);

                if (!_queue.TryEnqueue(new BlockRunJob(run, start.Program, startIds, Eval.Plain(start.Input), cancelled, start.Reply)))
                {
                    await ForgetAsync(run);
                    _registry.Release(run.RunId);
                    return Failure<BlockRun>(BlockRunErrors.QueueFull, "Too many runs are waiting; try again later.");
                }
            }
            catch
            {
                _registry.Release(run.RunId);
                throw;
            }

            return ServiceResult<BlockRun>.Success(run);
        }

        public async Task<IServiceResult<BlockRunPage>> ListAsync(
            Guid userId, int limit, string? cursor, CancellationToken ct = default)
        {
            byte[]? pagingState = null;
            if (cursor is not null)
            {
                try
                {
                    pagingState = WebEncoders.Base64UrlDecode(cursor);
                }
                catch (FormatException)
                {
                    return Failure<BlockRunPage>(BlockRunErrors.Invalid, "Invalid cursor.");
                }
            }

            try
            {
                var page = await _db.BlockRuns
                    .Where(r => r.UserId == userId)
                    .ToPageAsync(Math.Clamp(limit, 1, MaxPageSize), pagingState, ct);

                return ServiceResult<BlockRunPage>.Success(new BlockRunPage(
                    page.Items,
                    page.NextPagingState is { } next ? WebEncoders.Base64UrlEncode(next) : null));
            }
            // A tampered paging state is rejected by the server.
            catch (DriverException) when (pagingState is not null)
            {
                return Failure<BlockRunPage>(BlockRunErrors.Invalid, "Invalid cursor.");
            }
        }

        public async Task<IServiceResult<BlockRunDetails>> GetAsync(
            Guid userId, Guid runId, CancellationToken ct = default)
        {
            var run = await FindAsync(userId, runId, ct);
            if (run is null)
                return Failure<BlockRunDetails>(BlockRunErrors.NotFound, "Run not found.");

            var result = await _db.BlockRunResults
                .Where(r => r.UserId == userId && r.RunId == runId)
                .FirstOrDefaultAsync(ct);

            JsonElement? payload = null;
            if (result is not null)
            {
                using var doc = JsonDocument.Parse(result.Payload);
                payload = doc.RootElement.Clone();
            }

            return ServiceResult<BlockRunDetails>.Success(new BlockRunDetails(run, payload));
        }

        /// <summary>
        /// Starts a run that did not succeed again, with the same start blocks
        /// and input: with the program as it ran, or, for a workflow's run, its
        /// currently saved version (n8n's "retry with current workflow").
        /// Possible while the run's result is kept.
        /// </summary>
        public async Task<IServiceResult<BlockRun>> RetryAsync(
            Guid userId, Guid? actorId, Guid runId, bool currentWorkflow, CancellationToken ct = default)
        {
            var run = await FindAsync(userId, runId, ct);
            if (run is null)
                return Failure<BlockRun>(BlockRunErrors.NotFound, "Run not found.");
            if (!BlockRunStatus.IsFinished(run.Status))
                return Failure<BlockRun>(BlockRunErrors.NotFinished, "The run has not finished yet.");
            if (run.Status == BlockRunStatus.Succeeded)
                return Failure<BlockRun>(BlockRunErrors.Invalid, "Only runs that did not succeed can be retried.");

            var result = await _db.BlockRunResults
                .Where(r => r.UserId == userId && r.RunId == runId)
                .FirstOrDefaultAsync(ct);
            if (result is null)
                return Failure<BlockRun>(BlockRunErrors.NotFound, "The run's result has expired; start it again instead.");

            using var payload = JsonDocument.Parse(result.Payload);
            if (!payload.RootElement.TryGetProperty("start", out var start) || start.ValueKind != JsonValueKind.Object)
                return Failure<BlockRun>(BlockRunErrors.NotFound, "The run's start was too large to keep; start it again instead.");

            BlockProgram program;
            if (currentWorkflow)
            {
                if (run.WorkflowId is not { } workflowId)
                    return Failure<BlockRun>(BlockRunErrors.Invalid, "The run is not a workflow's; retry it as it ran.");
                var current = await _workflows.FindAsync(userId, workflowId, ct);
                if (current is null)
                    return Failure<BlockRun>(BlockRunErrors.NotFound, "The run's workflow no longer exists.");
                program = current;
            }
            else
            {
                program = start.GetProperty("program").Deserialize<BlockProgram>(BlockParamsBinder.Options) ?? new BlockProgram();
            }

            var startIds = start.GetProperty("startIds").Deserialize<List<int>>() ?? [];
            var input = start.TryGetProperty("input", out var stored) ? stored.Clone() : (JsonElement?)null;

            return await StartAsync(userId, actorId,
                new BlockRunStart(program, startIds, input, run.WorkflowId, BlockRunTriggers.Retry), ct);
        }

        public async Task<IServiceResult> CancelAsync(Guid userId, Guid runId, CancellationToken ct = default)
        {
            if (_registry.TryCancel(userId, runId))
                return ServiceResult.Success();

            var run = await FindAsync(userId, runId, ct);
            return run is null
                ? ServiceResult.Failure(e => e.Set(BlockRunErrors.NotFound, "Run not found."))
                : ServiceResult.Failure(e => e.Set(BlockRunErrors.AlreadyFinished, "Run has already finished."));
        }

        private Task<BlockRun?> FindAsync(Guid userId, Guid runId, CancellationToken ct)
            => _db.BlockRuns
                .Where(r => r.UserId == userId && r.RunId == runId)
                .FirstOrDefaultAsync(ct);

        private WriteOptions History => new(Ttl: _options.HistoryTtl);

        // Every issue, so a program can be fixed in one go; capped for the response.
        private static string Describe(IReadOnlyList<ProgramIssue> issues)
            => string.Join(" ", issues.Take(20).Select(i =>
                i.BlockId is { } id ? $"Block {id}: {i.Message}" : i.Message));

        // The run never reached the queue: leave no trace of it.
        private async Task ForgetAsync(BlockRun run)
        {
            _db.BlockRuns.Remove(run);
            _db.ActiveBlockRuns.Remove(new ActiveBlockRun { RunId = run.RunId, UserId = run.UserId });
            await _db.SaveChangesAsync(CancellationToken.None);
        }

        private static IServiceResult<T> Failure<T>(string code, string message)
            => ServiceResult<T>.Failure(e => e.Set(code, message));
    }
}
