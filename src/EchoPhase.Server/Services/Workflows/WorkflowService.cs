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

    /// <summary>A workflow's export file and what names it.</summary>
    /// <param name="FileName">workflow name and version, safe as a file name.</param>
    /// <param name="ContentHash">The program's hash: the file's ETag.</param>
    public sealed record WorkflowExport(string FileName, string Json, string ContentHash);

    /// <summary>A workflow to create with others in one package, its key chosen beforehand so they can call each other.</summary>
    public sealed record NewWorkflow(Guid Key, string Name, BlockProgram Program);

    /// <summary>A workflow made from an export file.</summary>
    /// <param name="Intact">The program matches the hash the file states (false: it was edited by hand).</param>
    public sealed record WorkflowImport(WorkflowDetails Details, bool Intact);

    /// <summary>
    /// Saved block programs, each in one of its owner's packages: storing
    /// them (validated: what they call is in their package), keeping their
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
        private readonly IWorkflowTriggerReloader _discordTriggers;

        public WorkflowService(
            WorkflowRepository workflows,
            UserRepository users,
            BlockRunService runs,
            BlockProgramValidator validator,
            IOptions<RunnerOptions> options,
            TimeProvider time,
            IWorkflowTriggerReloader discordTriggers)
        {
            _workflows = workflows;
            _users = users;
            _runs = runs;
            _validator = validator;
            _options = options.Value.Blocks;
            _time = time;
            _discordTriggers = discordTriggers;
        }

        /// <summary>The user's workflows, of one package or all.</summary>
        public Task<List<Workflow>> ListAsync(Guid userId, Guid? packageId = null, CancellationToken ct = default)
        {
            var query = _workflows.Query().WithUserIds(userId);
            if (packageId is { } package)
                query = query.WithPackageIds(package);
            return query.OrderBy(w => w.Name).ToListAsync(ct);
        }

        public async Task<IServiceResult<WorkflowDetails>> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
        {
            var workflow = await FindAsync(userId, id, ct);
            return workflow is null
                ? Failure<WorkflowDetails>(WorkflowErrors.NotFound, "Workflow not found.")
                : ServiceResult<WorkflowDetails>.Success(new WorkflowDetails(workflow, Parse(workflow)));
        }

        /// <summary>
        /// A new workflow in one of the user's packages; without one, in a new
        /// package of its own, of the same name.
        /// </summary>
        public async Task<IServiceResult<WorkflowDetails>> CreateAsync(
            Guid userId, Guid? actorId, Guid? packageId, string name, BlockProgram program, bool active, CancellationToken ct = default)
        {
            if (Check(name, program) is { } invalid)
                return Failure<WorkflowDetails>(WorkflowErrors.Invalid, invalid);

            WorkflowPackage package;
            if (packageId is { } id)
            {
                if (await _workflows.PackageAsync(userId, id, ct) is not { } owned)
                    return Failure<WorkflowDetails>(WorkflowErrors.NotFound, "Package not found.");
                package = owned;
            }
            else
            {
                var now = _time.GetUtcNow().UtcDateTime;
                package = new WorkflowPackage { OwnerId = userId, Name = name.Trim(), CreatedAt = now, UpdatedAt = now };
                _workflows.AddPackage(package);
            }

            var workflow = new Workflow
            {
                UserId = userId,
                PackageId = package.Id,
                Name = name.Trim(),
                Definition = Serialize(program),
                ContentHash = BlockProgramHash.Of(program),
                Active = active
            };
            if (await CheckLinksAsync(workflow, program, package, ct) is { } badLink)
                return Failure<WorkflowDetails>(WorkflowErrors.Invalid, badLink);
            _workflows.Add(workflow);
            _workflows.AddVersion(Snapshot(workflow, actorId ?? userId, parent: null, restoredFrom: null));
            await _workflows.ReplaceSchedulesAsync(workflow.Id, await SchedulesAsync(workflow, program, ct), ct);
            await _workflows.SaveAsync(ct);
            await _discordTriggers.ReloadUserAsync(userId, ct);

            return ServiceResult<WorkflowDetails>.Success(new WorkflowDetails(workflow, program));
        }

        /// <summary>
        /// Several new workflows in one package (tracked, perhaps new), inactive,
        /// saved together with it or not at all; they may call each other.
        /// </summary>
        public async Task<IServiceResult<IReadOnlyList<WorkflowDetails>>> CreateManyAsync(
            Guid userId, Guid? actorId, WorkflowPackage package, IReadOnlyList<NewWorkflow> items, CancellationToken ct = default)
        {
            var pending = items.ToDictionary(i => i.Key, i => i.Program);
            var created = new List<WorkflowDetails>();
            foreach (var item in items)
            {
                if (Check(item.Name, item.Program) is { } invalid)
                    return Failure<IReadOnlyList<WorkflowDetails>>(WorkflowErrors.Invalid, $"\"{item.Name}\": {invalid}");

                var workflow = new Workflow
                {
                    UserId = userId,
                    PackageId = package.Id,
                    Key = item.Key,
                    Name = item.Name.Trim(),
                    Definition = Serialize(item.Program),
                    ContentHash = BlockProgramHash.Of(item.Program),
                };
                if (await CheckLinksAsync(workflow, item.Program, package, ct, pending) is { } badLink)
                    return Failure<IReadOnlyList<WorkflowDetails>>(WorkflowErrors.Invalid, $"\"{item.Name}\": {badLink}");
                created.Add(new WorkflowDetails(workflow, item.Program));
            }

            foreach (var (workflow, program) in created)
            {
                _workflows.Add(workflow);
                _workflows.AddVersion(Snapshot(workflow, actorId ?? userId, parent: null, restoredFrom: null));
                await _workflows.ReplaceSchedulesAsync(workflow.Id, await SchedulesAsync(workflow, program, ct), ct);
            }
            await _workflows.SaveAsync(ct);
            return ServiceResult<IReadOnlyList<WorkflowDetails>>.Success(created);
        }

        /// <summary>
        /// Replaces the workflow. <paramref name="expectedVersion"/> guards
        /// against overwriting an edit made elsewhere (409 when it differs).
        /// </summary>
        public Task<IServiceResult<WorkflowDetails>> UpdateAsync(
            Guid userId, Guid? actorId, Guid id, string name, BlockProgram program, bool active, int? expectedVersion,
            CancellationToken ct = default)
            => SaveAsync(userId, actorId, id, name, program, active, expectedVersion, restoredFrom: null, ct);

        // A new version when the program changed (by its hash), linked to the one before it.
        private async Task<IServiceResult<WorkflowDetails>> SaveAsync(
            Guid userId, Guid? actorId, Guid id, string name, BlockProgram program, bool active, int? expectedVersion,
            int? restoredFrom, CancellationToken ct)
        {
            var workflow = await FindAsync(userId, id, ct);
            if (workflow is null)
                return Failure<WorkflowDetails>(WorkflowErrors.NotFound, "Workflow not found.");
            if (expectedVersion is { } expected && expected != workflow.Version)
                return Failure<WorkflowDetails>(WorkflowErrors.Conflict,
                    $"The workflow changed (version {workflow.Version}, not {expected}); reload it.");
            if (Check(name, program) is { } invalid)
                return Failure<WorkflowDetails>(WorkflowErrors.Invalid, invalid);
            if (await _workflows.PackageAsync(userId, workflow.PackageId, ct) is not { } package)
                throw new InvalidOperationException($"Workflow {workflow.Id} has no package of its owner's.");
            if (await CheckLinksAsync(workflow, program, package, ct) is { } badLink)
                return Failure<WorkflowDetails>(WorkflowErrors.Invalid, badLink);

            var hash = BlockProgramHash.Of(program);
            // Saved before hashes: its hash, from the stored program.
            var currentHash = workflow.ContentHash ?? BlockProgramHash.OfJson(workflow.Definition);
            bool changed = hash != currentHash;
            var parent = workflow.Version;
            if (changed)
                workflow.Version++;
            workflow.Name = name.Trim();
            workflow.Definition = Serialize(program);
            workflow.ContentHash = hash;
            workflow.Active = active;
            if (changed)
                _workflows.AddVersion(Snapshot(workflow, actorId ?? userId, parent, restoredFrom));

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
                : ServiceResult<WorkflowVersionDetails>.Success(new WorkflowVersionDetails(saved, BlockProgramJson.Parse(saved.Definition)));
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

            if (await FindAsync(userId, id, ct) is not { } workflow)
                return ServiceResult<WorkflowDetails>.Failure(e => e.Set(WorkflowErrors.NotFound, "Workflow not found."));
            return await SaveAsync(userId, actorId, id, saved.Value.Version.Name, saved.Value.Program,
                workflow.Active, expectedVersion, restoredFrom: version, ct);
        }

        /// <summary>
        /// The workflow (or one of its saved versions) as an export file, the
        /// same as `workflow export` writes; it imports on any server.
        /// </summary>
        public async Task<IServiceResult<WorkflowExport>> ExportAsync(Guid userId, Guid id, int? version, CancellationToken ct = default)
        {
            string name;
            BlockProgram program;
            int number;
            if (version is { } wanted)
            {
                var saved = await VersionAsync(userId, id, wanted, ct);
                if (!saved.Successful)
                    return ServiceResult<WorkflowExport>.Failure(saved.Error);
                (name, program, number) = (saved.Value.Version.Name, saved.Value.Program, saved.Value.Version.Version);
            }
            else
            {
                if (await FindAsync(userId, id, ct) is not { } workflow)
                    return Failure<WorkflowExport>(WorkflowErrors.NotFound, "Workflow not found.");
                (name, program, number) = (workflow.Name, Parse(workflow), workflow.Version);
            }

            var json = WorkflowFile.Write(name, program, number, _time.GetUtcNow());
            return ServiceResult<WorkflowExport>.Success(
                new WorkflowExport($"{FileNameOf(name)}-v{number}.json", json, BlockProgramHash.Of(program)));
        }

        /// <summary>
        /// A new workflow from an export file, inactive: its triggers start once
        /// it is turned on. Validated like any save; in the package given, or a
        /// new one of its own.
        /// </summary>
        /// <param name="name">Instead of the file's name.</param>
        public async Task<IServiceResult<WorkflowImport>> ImportAsync(Guid userId, Guid? actorId, string json, string? name,
            Guid? packageId = null, CancellationToken ct = default)
        {
            WorkflowFileContent file;
            try
            {
                file = WorkflowFile.Parse(json);
            }
            catch (FormatException e)
            {
                return Failure<WorkflowImport>(WorkflowErrors.Invalid, e.Message);
            }

            var created = await CreateAsync(userId, actorId, packageId, string.IsNullOrWhiteSpace(name) ? file.Name : name,
                file.Program, active: false, ct);
            return created.Successful
                ? ServiceResult<WorkflowImport>.Success(new WorkflowImport(created.Value, file.Intact))
                : ServiceResult<WorkflowImport>.Failure(created.Error);
        }

        // Letters, digits, - and _: a name any system takes as a file name.
        private static string FileNameOf(string name)
        {
            var safe = new string(name.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
            return safe.Length == 0 ? "workflow" : safe.Length > 60 ? safe[..60] : safe;
        }

        /// <summary>
        /// Deletes the workflow; not while another workflow of its package calls
        /// it or it is the package's error workflow (409): that would break them.
        /// </summary>
        public async Task<IServiceResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
        {
            var workflow = await FindAsync(userId, id, ct);
            if (workflow is null)
                return ServiceResult.Failure(e => e.Set(WorkflowErrors.NotFound, "Workflow not found."));
            if (await UsedByAsync(workflow, ct) is { } used)
                return ServiceResult.Failure(e => e.Set(WorkflowErrors.Conflict, used));

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
                new BlockRunStart(Parse(workflow), Input: input, Workflow: RunOf(workflow)), ct);
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
                new BlockRunStart(program, [blockId], body, RunOf(workflow), BlockRunTriggers.Webhook, reply), ct);

            if (started.TryGetValue(out var run))
                return ServiceResult<WebhookStarted>.Success(new WebhookStarted(run, reply));
            return started.Successful
                ? throw new InvalidOperationException($"Starting a run of workflow {id} succeeded without a run.")
                : ServiceResult<WebhookStarted>.Failure(e => e.Set(started.Error.Code, started.Error.Message));
        }

        /// <summary>
        /// After an automatic run of a workflow failed: starts the ErrorTriggers
        /// of its package's error workflow, active or not; null when the
        /// package has none. Their input describes the failed run.
        /// </summary>
        public async Task<IServiceResult<BlockRun?>> StartErrorWorkflowAsync(
            BlockRun failed, IReadOnlyList<BlockError> errors, CancellationToken ct = default)
        {
            if (failed.WorkflowId is not { } failedWorkflowId)
                return Failure<BlockRun?>(WorkflowErrors.Invalid, $"Run {failed.RunId} is not a workflow's; it has no error workflow.");

            var source = await _workflows.Query().WithIds(failedWorkflowId).WithUserIds(failed.UserId).FirstOrDefaultAsync(ct);
            if (source is null)
                return Failure<BlockRun?>(WorkflowErrors.NotFound, $"Workflow {failedWorkflowId} no longer exists.");
            var package = await _workflows.PackageAsync(failed.UserId, source.PackageId, ct);
            if (package?.ErrorWorkflowKey is not { } errorKey)
                return ServiceResult<BlockRun?>.Success(null);

            var errorWorkflow = errorKey == source.Key
                ? source
                : await _workflows.Query().WithUserIds(failed.UserId).WithPackageIds(source.PackageId).WithKeys(errorKey)
                    .FirstOrDefaultAsync(ct);
            if (errorWorkflow is null)
                return Failure<BlockRun?>(WorkflowErrors.NotFound, $"The error workflow {errorKey} of package {package.Id} is gone.");

            var program = Parse(errorWorkflow);
            var triggers = ErrorTriggerIds(program);
            if (triggers.Count == 0)
                return Failure<BlockRun?>(WorkflowErrors.Invalid, $"Error workflow {errorWorkflow.Id} has no enabled ErrorTrigger.");

            // A timeout or an oversized result fails a run without a block error.
            var described = errors.Count > 0
                ? errors.Select(e => (object?)e.ToValue()).ToList()
                : [new BlockError { Id = -1, Type = failed.Status, Message = $"The run ended as {failed.Status}." }.ToValue()];

            var input = new Dictionary<string, object?>
            {
                ["workflow"] = new Dictionary<string, object?>
                {
                    ["id"] = failedWorkflowId.ToString(),
                    ["key"] = source.Key.ToString(),
                    ["name"] = source.Name
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

            var started = await _runs.StartAsync(failed.UserId, null,
                new BlockRunStart(program, triggers, input, RunOf(errorWorkflow), BlockRunTriggers.Error), ct);
            return started.Successful
                ? ServiceResult<BlockRun?>.Success(started.Value)
                : ServiceResult<BlockRun?>.Failure(started.Error);
        }

        /// <summary>
        /// Sets (or clears) the package's error workflow: one of its workflows
        /// with an enabled ErrorTrigger, by key.
        /// </summary>
        public async Task<string?> CheckErrorWorkflowAsync(Guid userId, Guid packageId, Guid? key, CancellationToken ct = default)
        {
            if (key is not { } wanted)
                return null;
            var workflow = await _workflows.Query().WithUserIds(userId).WithPackageIds(packageId).WithKeys(wanted).FirstOrDefaultAsync(ct);
            if (workflow is null)
                return $"The package has no workflow {wanted}.";
            return ErrorTriggerIds(Parse(workflow)).Count == 0
                ? $"\"{workflow.Name}\" has no enabled ErrorTrigger: it cannot be the error workflow."
                : null;
        }

        private static RunWorkflow RunOf(Workflow workflow) => new(workflow.Id, workflow.PackageId);

        public static BlockProgram Parse(Workflow workflow) => BlockProgramJson.Parse(workflow.Definition);

        private static List<int> ErrorTriggerIds(BlockProgram program)
            => program.Blocks.Where(b => b.Type == BlockTypes.ErrorTrigger && !b.Disabled).Select(b => b.Id).ToList();

        // What the program calls must be in its package, caught on save, not
        // when a run gets there; it may call itself. The package's error
        // workflow must keep an ErrorTrigger.
        private async Task<string?> CheckLinksAsync(Workflow workflow, BlockProgram program, WorkflowPackage package,
            CancellationToken ct, IReadOnlyDictionary<Guid, BlockProgram>? pending = null)
        {
            if (package.ErrorWorkflowKey == workflow.Key && ErrorTriggerIds(program).Count == 0)
                return "This is its package's error workflow: keep an enabled ErrorTrigger, or choose another error workflow first.";

            var others = WorkflowLinks.Of(program).Workflows
                .Where(key => key != workflow.Key && pending?.ContainsKey(key) != true)
                .ToArray();
            if (others.Length == 0)
                return null;

            var found = (await _workflows.Query().WithUserIds(workflow.UserId).WithPackageIds(package.Id).WithKeys(others)
                .AsNoTracking().ToListAsync(ct)).Select(w => w.Key).ToHashSet();
            return others.FirstOrDefault(key => !found.Contains(key)) is var missing && missing != Guid.Empty
                ? $"It calls workflow {missing}, which is not in its package."
                : null;
        }

        // Why deleting the workflow would break its package, or null.
        private async Task<string?> UsedByAsync(Workflow workflow, CancellationToken ct)
        {
            var package = await _workflows.PackageAsync(workflow.UserId, workflow.PackageId, ct);
            if (package?.ErrorWorkflowKey == workflow.Key)
                return "It is its package's error workflow: choose another one (or none) first.";

            var siblings = await _workflows.Query().WithUserIds(workflow.UserId).WithPackageIds(workflow.PackageId).ToListAsync(ct);
            var caller = siblings.FirstOrDefault(w => w.Id != workflow.Id && WorkflowLinks.Of(Parse(w)).Workflows.Contains(workflow.Key));
            return caller is null ? null : $"\"{caller.Name}\" calls it: change that first.";
        }

        private WorkflowVersion Snapshot(Workflow workflow, Guid savedBy, int? parent, int? restoredFrom) => new()
        {
            WorkflowId = workflow.Id,
            Version = workflow.Version,
            Name = workflow.Name,
            Definition = workflow.Definition,
            ContentHash = workflow.ContentHash,
            ParentVersion = parent,
            RestoredFrom = restoredFrom,
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
                .SelectMany(t => t.Next is { } next
                    ? [new WorkflowSchedule
                    {
                        WorkflowId = workflow.Id,
                        BlockId = t.Id,
                        Cron = t.Cron,
                        TimeZone = zone,
                        NextRunAt = next.UtcDateTime
                    }]
                    : Array.Empty<WorkflowSchedule>())
                .ToList();
        }

        private Task<Workflow?> FindAsync(Guid userId, Guid id, CancellationToken ct)
            => _workflows.Query().WithIds(id).WithUserIds(userId).Include(w => w.Schedules).FirstOrDefaultAsync(ct);

        private static IServiceResult<T> Failure<T>(string code, string message)
            => ServiceResult<T>.Failure(e => e.Set(code, message));
    }
}
