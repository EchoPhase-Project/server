// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.Cli.Commands.Maintenance;
using EchoPhase.DAL.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.Cli.Commands.Prune
{
    public class PruneSettings : CommandSettings
    {
        [CommandOption("--older-than")]
        [DefaultValue("90d")]
        [Description("Age past which rows go: 30d, 90d...")]
        public string OlderThan { get; set; } = "90d";

        [CommandOption("--dry-run")]
        [Description("Only count what would be deleted")]
        public bool DryRun { get; set; }

        public override ValidationResult Validate()
            => Durations.TryParse(OlderThan, out var age) && age >= TimeSpan.FromDays(1)
                ? ValidationResult.Success()
                : ValidationResult.Error("--older-than takes a duration of at least 1d, such as 30d or 90d.");
    }

    /// <summary>
    /// Deletes what only grows: sessions not refreshed for the given age (their
    /// refresh tokens do not expire on their own, so this is the idle limit;
    /// the user signs in again), rotated refresh token hashes of that age
    /// (replaying one then reads as an unknown token instead of ending the
    /// session), and impersonations and permission grants that ended that long ago.
    /// Meant for a CronJob.
    /// </summary>
    public class PruneCommand : AsyncCommand<PruneSettings>
    {
        private readonly PostgresContext _db;
        private readonly TimeProvider _time;

        public PruneCommand(PostgresContext db, TimeProvider time)
        {
            _db = db;
            _time = time;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, PruneSettings settings, CancellationToken cancellationToken)
        {
            Durations.TryParse(settings.OlderThan, out var age);
            var cutoff = _time.GetUtcNow().UtcDateTime - age;

            var targets = new (string Name, IQueryable<object> Rows, Func<Task<int>> Delete)[]
            {
                ("Idle sessions",
                    _db.Sessions.Where(s => s.UpdatedAt < cutoff),
                    () => _db.Sessions.Where(s => s.UpdatedAt < cutoff).ExecuteDeleteAsync(cancellationToken)),
                ("Rotated refresh tokens",
                    _db.SessionAudits.Where(a => a.CreatedAt < cutoff),
                    () => _db.SessionAudits.Where(a => a.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken)),
                ("Ended impersonations",
                    _db.Impersonations.Where(i => (i.EndedAt ?? i.ExpiresAt) < cutoff),
                    () => _db.Impersonations.Where(i => (i.EndedAt ?? i.ExpiresAt) < cutoff).ExecuteDeleteAsync(cancellationToken)),
                ("Expired grants",
                    _db.UserPermissionGrants.Where(g => g.ExpiresAt < cutoff),
                    () => _db.UserPermissionGrants.Where(g => g.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken)),
            };

            var table = new Table().AddColumns("What", settings.DryRun ? "Would delete" : "Deleted");
            foreach (var (name, rows, delete) in targets)
            {
                var count = settings.DryRun ? await rows.CountAsync(cancellationToken) : await delete();
                table.AddRow(name, count.ToString());
            }
            table.Caption($"older than {cutoff:u}");
            AnsiConsole.Write(table);
            return CliExit.Success;
        }
    }
}
