// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Runners.Blocks.Models;
using Microsoft.EntityFrameworkCore;

// The workflow commands that save through IWorkflowEditor (validation,
// schedules). Triggers and schedules are read by the running server, so a
// change made here takes effect within a minute (its next reload), not at once.
namespace EchoPhase.Cli.Commands.Workflows
{
    public class ImportSettings : CommandSettings
    {
        [CommandArgument(0, "[FILE]")]
        [Description("JSON from `workflow export` ({ name, program }); stdin when left out")]
        public string? File { get; set; }

        [CommandOption("--user|-u")]
        [Description("Owner of the new workflow")]
        public string User { get; set; } = "";

        [CommandOption("--name")]
        [Description("Name instead of the one in the file")]
        public string? Name { get; set; }

        [CommandOption("--package")]
        [Description("Id of the owner's package it goes in; left out: a new package of its own")]
        public Guid? Package { get; set; }

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(User) ? ValidationResult.Error("--user is required.") : ValidationResult.Success();
    }

    /// <summary>
    /// Creates a workflow from an export, inactive: its triggers run once the
    /// owner (or `workflow enable`) turns it on, after checking its connections.
    /// </summary>
    public class ImportCommand : AsyncCommand<ImportSettings>
    {
        private readonly UserRepository _users;
        private readonly IWorkflowEditor _workflows;

        public ImportCommand(UserRepository users, IWorkflowEditor workflows)
        {
            _users = users;
            _workflows = workflows;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, ImportSettings settings, CancellationToken cancellationToken)
        {
            var owner = (await _users.Query().WithUserNames(settings.User.Trim()).ToListAsync(cancellationToken)).FirstOrDefault();
            if (owner is null)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]No user '{settings.User}'.[/]");
                return CliExit.Failure;
            }

            string name;
            BlockProgram program;
            try
            {
                var json = string.IsNullOrEmpty(settings.File)
                    ? await Console.In.ReadToEndAsync(cancellationToken)
                    : await File.ReadAllTextAsync(settings.File, cancellationToken);
                var file = WorkflowFile.Parse(json);
                (name, program) = (file.Name, file.Program);
                if (!file.Intact)
                    AnsiConsole.MarkupLine("[yellow]The program does not match the file's contentHash: it was edited after export.[/]");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
                return CliExit.Failure;
            }

            var result = await _workflows.CreateInactiveAsync(owner.Id, settings.Package, settings.Name ?? name, program,
                cancellationToken);
            if (!result.Successful)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{result.Error.Message}[/]");
                return CliExit.Failure;
            }

            AnsiConsole.MarkupLineInterpolated(
                $"[green]Imported '{result.Value.Name}' for '{settings.User}' as {result.Value.Id} (package {result.Value.PackageId}), inactive.[/]");
            return CliExit.Success;
        }
    }

    public class ActivateSettings : WorkflowSettings
    {
    }

    /// <summary>Turns a workflow's triggers on (or off) without touching its program.</summary>
    public abstract class ActivateCommandBase : AsyncCommand<ActivateSettings>
    {
        private readonly WorkflowRepository _repository;
        private readonly IWorkflowEditor _workflows;

        protected ActivateCommandBase(WorkflowRepository repository, IWorkflowEditor workflows)
        {
            _repository = repository;
            _workflows = workflows;
        }

        protected abstract bool Active { get; }

        public override async Task<int> ExecuteAsync(CommandContext context, ActivateSettings settings, CancellationToken cancellationToken)
        {
            var workflow = await _repository.Query().WithIds(settings.Id).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (workflow is null)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]No workflow {settings.Id}.[/]");
                return CliExit.Failure;
            }

            var result = await _workflows.SetActiveAsync(workflow, Active, cancellationToken);
            if (!result.Successful)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{result.Error.Message}[/]");
                return CliExit.Failure;
            }

            AnsiConsole.MarkupLineInterpolated(
                $"[green]'{workflow.Name}' is {(Active ? "active" : "inactive")}; the server applies it within a minute.[/]");
            return CliExit.Success;
        }
    }

    public class EnableCommand : ActivateCommandBase
    {
        public EnableCommand(WorkflowRepository repository, IWorkflowEditor workflows)
            : base(repository, workflows)
        {
        }

        protected override bool Active => true;
    }

    public class DisableCommand : ActivateCommandBase
    {
        public DisableCommand(WorkflowRepository repository, IWorkflowEditor workflows)
            : base(repository, workflows)
        {
        }

        protected override bool Active => false;
    }

    /// <summary>Prints a workflow as an export file (the API's format); `workflows import` reads it back.</summary>
    public class ExportCommand : AsyncCommand<WorkflowSettings>
    {
        private readonly WorkflowRepository _repository;
        private readonly TimeProvider _time;

        public ExportCommand(WorkflowRepository repository, TimeProvider time)
        {
            _repository = repository;
            _time = time;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, WorkflowSettings settings, CancellationToken cancellationToken)
        {
            var workflow = await _repository.Query().WithIds(settings.Id).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (workflow is null)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]No workflow {settings.Id}.[/]");
                return CliExit.Failure;
            }

            var program = BlockProgramJson.Parse(workflow.Definition);
            Console.Out.WriteLine(WorkflowFile.Write(workflow.Name, program, workflow.Version, _time.GetUtcNow()));
            return CliExit.Success;
        }
    }
}
