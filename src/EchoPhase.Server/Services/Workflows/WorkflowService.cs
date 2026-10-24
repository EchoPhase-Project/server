// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoPhase.Configuration.Runner;
using EchoPhase.Configuration.Runner.Blocks;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Runners.Blocks.Triggers;
using EchoPhase.Runners.Blocks.Validation;
using EchoPhase.Server.Services.Blocks;
using EchoPhase.Types.Result;
using EchoPhase.Types.Result.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Services.Workflows
{
    public static class WorkflowErrors
    {
        public const string NotFound = ErrorCodes.NotFound;
        public const string Invalid = ErrorCodes.Invalid;
        public const string Conflict = ErrorCodes.Conflict;
        public const string Unauthorized = ErrorCodes.Unauthorized;
    }

    /// <summary>A workflow with its definition parsed.</summary>
    public sealed record WorkflowDetails(Workflow Workflow, BlockProgram Program);

    /// <summary>A run started by a webhook, and its caller's pending answer when the trigger waits.</summary>
    public sealed record WebhookStarted(BlockRun Run, WebhookReply? Reply);

    /// <summary>One saved version with its program.</summary>
    public sealed record WorkflowVersionDetails(WorkflowVersion Version, BlockProgram Program);

    /// <summary>
    /// Saved block programs: storing them (validated), keeping their
    /// schedules in step with their ScheduleTriggers, and starting runs of
    /// them by hand or from a webhook. Every lookup is by owner.
    /// </summary>
    public class WorkflowService
    {
        public const int MaxNameLength = 100;

        /// <summary>Versions kept per workflow; older ones are deleted on save.</summary>
        public const int MaxVersions = 50;

        private readonly WorkflowRepository _workflows;
        private readonly UserRepository _users;
        private readonly BlockRunService _runs;
        private readonly BlockProgramValidator _validator;
        private readonly BlocksOptions _options;
        private readonly TimeProvider _time;
        private readonly DiscordTriggerService _discordTriggers;

        public WorkflowService(
            WorkflowRepository workflows,
            UserRepository users,
            BlockRunService runs,
            BlockProgramValidator validator,
            IOptions<RunnerOptions> options,
            TimeProvider time,
            DiscordTriggerService discordTriggers)
        {
            _workflows = workflows;
            _users = users;
            _runs = runs;
            _validator = validator;
            _options = options.Value.Blocks;
            _time = time;
            _discordTriggers = discordTriggers;
        }

        public Task<List<Workflow>> ListAsync(Guid userId, CancellationToken ct = default)
            => _workflows.Query().WithUserIds(userId).OrderBy(w => w.Name).ToListAsync(ct);

        public async Task<IServiceResult<WorkflowDetails>> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
        {
            var workflow = await FindAsync(userId, id, ct);
            return workflow is null
                ? Failure<WorkflowDetails>(WorkflowErrors.NotFound, "Workflow not found.")
                : ServiceResult<WorkflowDetails>.Success(new WorkflowDetails(workflow, Parse(workflow)));
        }

        public async Task<IServiceResult<WorkflowDetails>> CreateAsync(
            Guid userId, Guid? actorId, string name, BlockProgram program, bool active, CancellationToken ct = default)
        {
            if (Check(name, program) is { } invalid)
                return Failure<WorkflowDetails>(WorkflowErrors.Invalid, invalid);

            var workflow = new Workflow
            {
                UserId = userId,
                Name = name.Trim(),
                Definition = Serialize(program),
                Active = active
            };
            if (await CheckErrorWorkflowAsync(workflow, program, ct) is { } badErrorWorkflow)
                return Failure<WorkflowDetails>(WorkflowErrors.Invalid, badErrorWorkflow);
            _workflows.Add(workflow);
            _workflows.AddVersion(Snapshot(workflow, actorId ?? userId));
            await _workflows.ReplaceSchedulesAsync(workflow.Id, await SchedulesAsync(workflow, program, ct), ct);
            await _workflows.SaveAsync(ct);
            await _discordTriggers.ReloadUserAsync(userId, ct);

            return ServiceResult<WorkflowDetails>.Success(new WorkflowDetails(workflow, program));
        }

        /// <summary>
        /// Replaces the workflow. <paramref name="expectedVersion"/> guards
        /// against overwriting an edit made elsewhere (409 when it differs).
        /// </summary>
        public async Task<IServiceResult<WorkflowDetails>> UpdateAsync(
            Guid userId, Guid? actorId, Guid id, string name, BlockProgram program, bool active, int? expectedVersion,
            CancellationToken ct = default)
        {
            var workflow = await FindAsync(userId, id, ct);
            if (workflow is null)
                return Failure<WorkflowDetails>(WorkflowErrors.NotFound, "Workflow not found.");
            if (expectedVersion is { } expected && expected != workflow.Version)
                return Failure<WorkflowDetails>(WorkflowErrors.Conflict,
                    $"The workflow changed (version {workflow.Version}, not {expected}); reload it.");
            if (Check(name, program) is { } invalid)
                return Failure<WorkflowDetails>(WorkflowErrors.Invalid, invalid);
            if (await CheckErrorWorkflowAsync(workflow, program, ct) is { } badErrorWorkflow)
                return Failure<WorkflowDetails>(WorkflowErrors.Invalid, badErrorWorkflow);

            var definition = Serialize(program);
            bool changed = definition != workflow.Definition;
            if (changed)
                workflow.Version++;
            workflow.Name = name.Trim();
            workflow.Definition = definition;
            workflow.Active = active;
            if (changed)
                _workflows.AddVersion(Snapshot(workflow, actorId ?? userId));

            await _workflows.ReplaceSchedulesAsync(workflow.Id, await SchedulesAsync(workflow, program, ct), ct);
            try
            {
                await _workflows.SaveAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Failure<WorkflowDetails>(WorkflowErrors.Conflict, "The workflow changed meanwhile; reload it.");
            }

            if (changed)
                await _workflows.PruneVersionsAsync(workflow.Id, MaxVersions, ct);
            await _discordTriggers.ReloadUserAsync(userId, ct);

            return ServiceResult<WorkflowDetails>.Success(new WorkflowDetails(workflow, program));
        }

        /// <summary>Saved versions, newest first (definitions not included).</summary>
        public async Task<IServiceResult<List<WorkflowVersion>>> VersionsAsync(Guid userId, Guid id, CancellationToken ct = default)
        {
            var workflow = await FindAsync(userId, id, ct);
            return workflow is null
                ? Failure<List<WorkflowVersion>>(WorkflowErrors.NotFound, "Workflow not found.")
                : ServiceResult<List<WorkflowVersion>>.Success(await _workflows.VersionsAsync(id, ct));
        }

        public async Task<IServiceResult<WorkflowVersionDetails>> VersionAsync(
            Guid userId, Guid id, int version, CancellationToken ct = default)
        {
            var workflow = await FindAsync(userId, id, ct);
            if (workflow is null)
                return Failure<WorkflowVersionDetails>(WorkflowErrors.NotFound, "Workflow not found.");

            var saved = await _workflows.VersionAsync(id, version, ct);
            return saved is null
                ? Failure<WorkflowVersionDetails>(WorkflowErrors.NotFound, $"Version {version} not found.")
                : ServiceResult<WorkflowVersionDetails>.Success(new WorkflowVersionDetails(saved, Parse(saved.Definition)));
        }

        /// <summary>
        /// Makes an old version current again, as a new version on top (history
        /// is never rewritten). Validated like any save: the old program must
        /// still be valid with today's block types.
        /// </summary>
        public async Task<IServiceResult<WorkflowDetails>> RestoreAsync(
            Guid userId, Guid? actorId, Guid id, int version, int? expectedVersion, CancellationToken ct = default)
        {
            var saved = await VersionAsync(userId, id, version, ct);
            if (!saved.Successful)
                return ServiceResult<WorkflowDetails>.Failure(e => e.Set(saved.Error.Code, saved.Error.Message));

            var workflow = (await FindAsync(userId, id, ct))!;
            return await UpdateAsync(userId, actorId, id, saved.Value!.Version.Name, saved.Value.Program,
                workflow.Active, expectedVersion, ct);
        }

        public async Task<IServiceResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
        {
            var workflow = await FindAsync(userId, id, ct);
            if (workflow is null)
                return ServiceResult.Failure(e => e.Set(WorkflowErrors.NotFound, "Workflow not found."));

            // Schedules go with it (cascade).
            _workflows.Remove(workflow);
            await _workflows.SaveAsync(ct);
            await _discordTriggers.ReloadUserAsync(userId, ct);
            return ServiceResult.Success();
        }

        /// <summary>A run by hand, active or not: from its ManualTriggers (or roots) with the caller's input.</summary>
        public async Task<IServiceResult<BlockRun>> RunAsync(
            Guid userId, Guid? actorId, Guid id, object? input, CancellationToken ct = default)
        {
            var workflow = await FindAsync(userId, id, ct);
            if (workflow is null)
                return Failure<BlockRun>(WorkflowErrors.NotFound, "Workflow not found.");

            return await _runs.StartAsync(userId, actorId,
                new BlockRunStart(Parse(workflow), Input: input, WorkflowId: workflow.Id), ct);
        }

        /// <summary>
        /// A POST to the webhook of block <paramref name="blockId"/>. Not found
        /// for a missing or inactive workflow or a block that is not a
        /// WebhookTrigger (indistinguishable on purpose); unauthorized for a
        /// wrong secret. The run belongs to the workflow's owner.
        /// </summary>
        public async Task<IServiceResult<WebhookStarted>> TriggerWebhookAsync(
            Guid id, int blockId, string? secret, object? body, CancellationToken ct = default)
        {
            var workflow = await _workflows.Query().WithIds(id).FirstOrDefaultAsync(ct);
            if (workflow is not { Active: true })
                return Failure<WebhookStarted>(WorkflowErrors.NotFound, "Webhook not found.");

            var program = Parse(workflow);
            var block = program.Blocks.FirstOrDefault(b => b.Id == blockId && b.Type == BlockTypes.WebhookTrigger && !b.Disabled);
            if (block is null)
                return Failure<WebhookStarted>(WorkflowErrors.NotFound, "Webhook not found.");

            var trigger = (WebhookTriggerParams)BlockParamsBinder.Bind(typeof(WebhookTriggerParams), block.Params);
            if (secret is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(trigger.Secret)))
                return Failure<WebhookStarted>(WorkflowErrors.Unauthorized, "Wrong webhook secret.");

            var reply = trigger.Respond == WebhookRespondMode.Immediately ? null : new WebhookReply(trigger.Respond);
            var started = await _runs.StartAsync(workflow.UserId, null,
                new BlockRunStart(program, [blockId], body, workflow.Id, BlockRunTriggers.Webhook, reply), ct);

            if (started.TryGetValue(out var run))
                return ServiceResult<WebhookStarted>.Success(new WebhookStarted(run, reply));
            return started.Successful
                ? throw new InvalidOperationException($"Starting a run of workflow {id} succeeded without a run.")
                : ServiceResult<WebhookStarted>.Failure(e => e.Set(started.Error.Code, started.Error.Message));
        }

        /// <summary>
        /// After an automatic run of a workflow failed: starts the ErrorTriggers
        /// of the error workflow it names, owned by the same user, active or
        /// not. Their input describes the failed run.
        /// </summary>
        public async Task<IServiceResult<BlockRun>> StartErrorWorkflowAsync(
            BlockRun failed, Guid errorWorkflowId, IReadOnlyList<BlockError> errors, CancellationToken ct = default)
        {
            if (failed.WorkflowId is not { } failedWorkflowId)
                return Failure<BlockRun>(WorkflowErrors.Invalid, $"Run {failed.RunId} is not a workflow's; it has no error workflow.");

            var errorWorkflow = await _workflows.Query().WithIds(errorWorkflowId).WithUserIds(failed.UserId).FirstOrDefaultAsync(ct);
            if (errorWorkflow is null)
                return Failure<BlockRun>(WorkflowErrors.NotFound, $"Error workflow {errorWorkflowId} not found.");

            var program = Parse(errorWorkflow);
            var triggers = ErrorTriggerIds(program);
            if (triggers.Count == 0)
                return Failure<BlockRun>(WorkflowErrors.Invalid, $"Error workflow {errorWorkflowId} has no enabled ErrorTrigger.");

            var source = failedWorkflowId == errorWorkflow.Id
                ? errorWorkflow
                : await _workflows.Query().WithIds(failedWorkflowId).FirstOrDefaultAsync(ct);

            // A timeout or an oversized result fails a run without a block error.
            var described = errors.Count > 0
                ? errors.Select(e => (object?)e.ToValue()).ToList()
                : [new BlockError { Id = -1, Type = failed.Status, Message = $"The run ended as {failed.Status}." }.ToValue()];

            var input = new Dictionary<string, object?>
            {
                ["workflow"] = new Dictionary<string, object?>
                {
                    ["id"] = failedWorkflowId.ToString(),
                    ["name"] = source?.Name
                },
                ["run"] = new Dictionary<string, object?>
                {
                    ["id"] = failed.RunId.ToString(),
                    ["trigger"] = failed.Trigger,
                    ["status"] = failed.Status,
                    ["startedAt"] = failed.StartedAt?.ToString("O"),
                    ["finishedAt"] = failed.FinishedAt?.ToString("O")
                },
                ["errors"] = described
            };

            return await _runs.StartAsync(failed.UserId, null,
                new BlockRunStart(program, triggers, input, errorWorkflow.Id, BlockRunTriggers.Error), ct);
        }

        public static BlockProgram Parse(Workflow workflow) => Parse(workflow.Definition);

        private static List<int> ErrorTriggerIds(BlockProgram program)
            => program.Blocks.Where(b => b.Type == BlockTypes.ErrorTrigger && !b.Disabled).Select(b => b.Id).ToList();

        // The error workflow must be the owner's and able to start: caught on
        // save, not when a run has already failed. It may be the workflow itself.
        private async Task<string?> CheckErrorWorkflowAsync(Workflow workflow, BlockProgram program, CancellationToken ct)
        {
            if (program.Settings.ErrorWorkflowId is not { } id)
                return null;

            var target = id == workflow.Id
                ? program
                : await _workflows.Query().WithIds(id).WithUserIds(workflow.UserId).FirstOrDefaultAsync(ct) is { } other
                    ? Parse(other)
                    : null;

            if (target is null)
                return $"Error workflow {id} not found.";
            if (ErrorTriggerIds(target).Count == 0)
                return $"Error workflow {id} has no enabled ErrorTrigger.";
            return null;
        }

        private static BlockProgram Parse(string definition)
            => JsonSerializer.Deserialize<BlockProgram>(definition, BlockParamsBinder.Options) ?? new BlockProgram();

        private WorkflowVersion Snapshot(Workflow workflow, Guid savedBy) => new()
        {
            WorkflowId = workflow.Id,
            Version = workflow.Version,
            Name = workflow.Name,
            Definition = workflow.Definition,
            SavedBy = savedBy,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };

        private static string Serialize(BlockProgram program)
            => JsonSerializer.Serialize(program, BlockParamsBinder.Options);

        private string? Check(string name, BlockProgram program)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
                return $"Name must be 1-{MaxNameLength} characters.";

            var issues = _validator.Validate(program, _options.MaxBlocks);
            return issues.Count == 0
                ? null
                : string.Join(" ", issues.Take(20).Select(i => i.BlockId is { } b ? $"Block {b}: {i.Message}" : i.Message));
        }

        // One schedule per enabled ScheduleTrigger, read in the program's zone,
        // else the owner's, else UTC.
        private async Task<List<WorkflowSchedule>> SchedulesAsync(Workflow workflow, BlockProgram program, CancellationToken ct)
        {
            var triggers = program.Blocks.Where(b => b.Type == BlockTypes.ScheduleTrigger && !b.Disabled).ToList();
            if (triggers.Count == 0)
                return [];

            var zone = program.Settings.TimeZone
                ?? (await _users.Query().WithIds(workflow.UserId).FirstOrDefaultAsync(ct))?.TimeZone
                ?? "UTC";
            var now = _time.GetUtcNow();

            // A cron that never fires again (e.g. 30 February) gets no row.
            return triggers
                .Select(b => (b.Id, ((ScheduleTriggerParams)BlockParamsBinder.Bind(typeof(ScheduleTriggerParams), b.Params)).Cron))
                .Select(t => (t.Id, t.Cron, Next: CronSchedule.Next(t.Cron, zone, now)))
                .Where(t => t.Next is not null)
                .Select(t => new WorkflowSchedule
                {
                    WorkflowId = workflow.Id,
                    BlockId = t.Id,
                    Cron = t.Cron,
                    TimeZone = zone,
                    NextRunAt = t.Next!.Value.UtcDateTime
                })
                .ToList();
        }

        private Task<Workflow?> FindAsync(Guid userId, Guid id, CancellationToken ct)
            => _workflows.Query().WithIds(id).WithUserIds(userId).Include(w => w.Schedules).FirstOrDefaultAsync(ct);

        private static IServiceResult<T> Failure<T>(string code, string message)
            => ServiceResult<T>.Failure(e => e.Set(code, message));
    }
}
