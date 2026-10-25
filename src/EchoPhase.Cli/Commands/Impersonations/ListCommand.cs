// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.Cli.Commands.Workflows;
using EchoPhase.DAL.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.Cli.Commands.Impersonations
{
    public class ListSettings : ListingSettings
    {
        [CommandOption("--user|-u")]
        [Description("Only where this user was the actor or the target")]
        public string? User { get; set; }

        [CommandOption("--active")]
        [Description("Only impersonations going on now")]
        public bool Active { get; set; }

        public override ValidationResult Validate()
        {
            if (After is not null && !Cursors.TryParseTime(After, out _))
                return ValidationResult.Error("--after takes the cursor printed under the previous page.");
            return base.Validate();
        }
    }

    public sealed record ImpersonationRow(
        Guid Id, string? Actor, string? Target, string Reason,
        DateTime StartedAt, DateTime ExpiresAt, DateTime? EndedAt);

    /// <summary>Who acted as whom and why, newest first: the audit trail of impersonation.</summary>
    public class ListCommand : AsyncCommand<ListSettings>
    {
        private readonly PostgresContext _db;
        private readonly TimeProvider _time;

        public ListCommand(PostgresContext db, TimeProvider time)
        {
            _db = db;
            _time = time;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings, CancellationToken cancellationToken)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var records = _db.Impersonations.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(settings.User))
            {
                var name = settings.User.Trim();
                records = records.Where(i => (i.Actor != null && i.Actor.UserName == name) || (i.Target != null && i.Target.UserName == name));
            }
            if (settings.Active)
                records = records.Where(i => i.EndedAt == null && i.ExpiresAt > now);

            var table = new Table().AddColumns("Started (UTC)", "Actor", "Target", "Reason", "Ended (UTC)");
            var next = await Listing.PagesAsync(settings,
                (after, take, ct) =>
                {
                    // Cursor and order on the entity: EF cannot translate them on the row.
                    var page = records;
                    if (after is not null && Cursors.TryParseTime(after, out var before))
                        page = page.Where(i => i.CreatedAt < before);
                    return page.OrderByDescending(i => i.CreatedAt).Take(take)
                        .Select(i => new ImpersonationRow(
                            i.Id, i.Actor != null ? i.Actor.UserName : null, i.Target != null ? i.Target.UserName : null, i.Reason, i.CreatedAt, i.ExpiresAt, i.EndedAt))
                        .ToListAsync(ct);
                },
                r => Cursors.Time(r.StartedAt),
                page =>
                {
                    foreach (var r in page)
                    {
                        if (settings.Json)
                        {
                            Listing.WriteJson(r);
                            continue;
                        }
                        var ended = r.EndedAt is { } at ? Listing.Time(at)
                            : r.ExpiresAt > now ? "[yellow]going on[/]"
                            : $"expired {Listing.Time(r.ExpiresAt)}";
                        table.AddRow(Listing.Time(r.StartedAt), Markup.Escape(r.Actor ?? "(deleted)"),
                            Markup.Escape(r.Target ?? "(deleted)"), Markup.Escape(r.Reason), ended);
                    }
                    return Task.CompletedTask;
                },
                cancellationToken);

            if (!settings.Json)
            {
                table.Caption($"{table.Rows.Count} shown of {await records.CountAsync(cancellationToken)}");
                AnsiConsole.Write(table);
            }
            Listing.Footer(settings, next);
            return CliExit.Success;
        }
    }
}
