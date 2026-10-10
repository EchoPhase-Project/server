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
using EchoPhase.Runners.Blocks.Runs;
using EchoPhase.Runners.Blocks.Validation;
using EchoPhase.Scripting;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using UUIDNext;

namespace EchoPhase.Services.Blocks
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

        public BlockRunService(
            ScyllaContext db,
            BlockRunQueue queue,
            BlockRunRegistry registry,
            IOptions<RunnerOptions> options,
            TimeProvider time,
            BlockProgramValidator validator)
        {
            _db = db;
            _queue = queue;
            _registry = registry;
            _options = options.Value.Blocks;
            _time = time;
            _validator = validator;
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

                if (!_queue.TryEnqueue(new BlockRunJob(run, start.Program, startIds, Eval.Plain(start.Input), cancelled)))
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
