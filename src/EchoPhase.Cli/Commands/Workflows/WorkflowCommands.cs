// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using EchoPhase.DAL.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.Cli.Commands.Workflows
{
    public class ListSettings : ListingSettings
    {
        [CommandOption("--user|-u")]
        [Description("Only this user's workflows")]
        public string? User { get; set; }

        [CommandOption("--active")]
        [Description("Only active workflows")]
        public bool Active { get; set; }

        public override ValidationResult Validate()
        {
            if (After is not null && !Cursors.TryParseTime(After, out _))
                return ValidationResult.Error("--after takes the cursor printed under the previous page.");
            return base.Validate();
        }
    }

    public sealed record WorkflowRow(
        Guid Id, string? Owner, string Name, bool Active, int Version,
        int Schedules, DateTime? NextRunAt, DateTime CreatedAt, DateTime UpdatedAt);

    /// <summary>Workflows of everyone (or one user), oldest first, with their next scheduled run.</summary>
    public class ListCommand : AsyncCommand<ListSettings>
    {
        private readonly PostgresContext _db;

        public ListCommand(PostgresContext db)
        {
            _db = db;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings, CancellationToken cancellationToken)
        {
            var workflows = _db.Workflows.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(settings.User))
            {
                var owner = settings.User.Trim();
                workflows = workflows.Where(w => w.User != null && w.User.UserName == owner);
            }
            if (settings.Active)
                workflows = workflows.Where(w => w.Active);

            var table = new Table().AddColumns("Id", "Owner", "Name", "Active", "Version", "Next run (UTC)", "Updated (UTC)");
            var next = await Listing.PagesAsync(settings,
                (after, take, ct) =>
                {
                    // Cursor and order on the entity: EF cannot translate them on the row.
                    var page = workflows;
                    if (after is not null && Cursors.TryParseTime(after, out var since))
                        page = page.Where(w => w.CreatedAt > since);
                    return page.OrderBy(w => w.CreatedAt).Take(take)
                        .Select(w => new WorkflowRow(
                            w.Id, w.User != null ? w.User.UserName : null, w.Name, w.Active, w.Version,
                            w.Schedules.Count(),
                            w.Active ? w.Schedules.Min(s => (DateTime?)s.NextRunAt) : null,
                            w.CreatedAt, w.UpdatedAt))
                        .ToListAsync(ct);
                },
                r => Cursors.Time(r.CreatedAt),
                page =>
                {
                    foreach (var r in page)
                    {
                        if (settings.Json)
                            Listing.WriteJson(r);
                        else
                            table.AddRow(r.Id.ToString(), Markup.Escape(r.Owner ?? ""), Markup.Escape(r.Name),
                                r.Active ? "yes" : "", r.Version.ToString(), Listing.Time(r.NextRunAt), Listing.Time(r.UpdatedAt));
                    }
                    return Task.CompletedTask;
                },
                cancellationToken);

            if (!settings.Json)
            {
                table.Caption($"{table.Rows.Count} shown of {await workflows.CountAsync(cancellationToken)}");
                AnsiConsole.Write(table);
            }
            Listing.Footer(settings, next);
            return CliExit.Success;
        }
    }

    public class WorkflowSettings : CommandSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("Workflow id (see `workflows`)")]
        public Guid Id { get; set; }
    }

    /// <summary>
    /// The workflow as JSON on stdout: { name, program }, the body the API's
    /// create takes, and what `workflow import` reads.
    /// </summary>
    public class ExportCommand : AsyncCommand<WorkflowSettings>
    {
        private readonly PostgresContext _db;

        public ExportCommand(PostgresContext db)
        {
            _db = db;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, WorkflowSettings settings, CancellationToken cancellationToken)
        {
            var workflow = await _db.Workflows.AsNoTracking()
                .Where(w => w.Id == settings.Id)
                .Select(w => new { w.Name, w.Definition })
                .FirstOrDefaultAsync(cancellationToken);
            if (workflow is null)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]No workflow {settings.Id}.[/]");
                return CliExit.Failure;
            }

            using var program = JsonDocument.Parse(workflow.Definition);
            Listing.WriteJsonDocument(new { name = workflow.Name, program = program.RootElement });
            return CliExit.Success;
        }
    }

    /// <summary>Cursors over creation times: exact to the tick, so no row is skipped or repeated.</summary>
    public static class Cursors
    {
        public static string Time(DateTime utc) => utc.ToString("O", CultureInfo.InvariantCulture);

        public static bool TryParseTime(string text, out DateTime utc)
        {
            var ok = DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return ok;
        }
    }
}
