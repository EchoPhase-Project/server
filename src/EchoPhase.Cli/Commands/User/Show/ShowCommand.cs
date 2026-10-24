// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.Cli.Commands.User.Show
{
    public class ShowSettings : UserSettings
    {
        [CommandOption("--json")]
        [Description("One JSON document instead of the tables")]
        public bool Json { get; set; }
    }

    public sealed record UserGrantRow(string Resource, string[] Permissions, string Reason, DateTime? ExpiresAt, bool Active);

    public sealed record UserDetails(
        Guid Id, string? UserName, string Name, string? Email, bool EmailConfirmed, string TimeZone,
        IReadOnlyList<string> Roles, IReadOnlyList<UserGrantRow> Grants,
        bool TwoFactor, bool LockoutEnabled, DateTimeOffset? LockedUntil, int FailedSignIns,
        int Sessions, DateTime? LastRefresh, int ApiClients, int Connections, int WebHooks,
        int Workflows, int ActiveWorkflows, DateTime CreatedAt, DateTime UpdatedAt);

    /// <summary>One user: account state, roles and grants, and how much they own.</summary>
    public class ShowCommand : AsyncCommand<ShowSettings>
    {
        private readonly UserRepository _users;
        private readonly PostgresContext _db;
        private readonly IUserPermissionService _permissions;
        private readonly TimeProvider _time;

        public ShowCommand(UserRepository users, PostgresContext db, IUserPermissionService permissions, TimeProvider time)
        {
            _users = users;
            _db = db;
            _permissions = permissions;
            _time = time;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, ShowSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            var now = _time.GetUtcNow();
            var view = await _permissions.DescribeAsync(user, cancellationToken);
            var sessions = _db.Sessions.Where(s => s.UserId == user.Id);
            var workflows = _db.Workflows.Where(w => w.UserId == user.Id);

            var details = new UserDetails(
                user.Id, user.UserName, user.Name, user.Email, user.EmailConfirmed, user.TimeZone,
                view.Roles,
                view.Grants.Select(g => new UserGrantRow(g.Resource, g.Permissions, g.Reason, g.ExpiresAt, g.IsActive(now.UtcDateTime))).ToList(),
                user.TwoFactorEnabled, user.LockoutEnabled,
                user.LockoutEnd > now ? user.LockoutEnd : null,
                user.AccessFailedCount,
                await sessions.CountAsync(cancellationToken),
                await sessions.MaxAsync(s => (DateTime?)s.UpdatedAt, cancellationToken),
                await _db.ApiClients.CountAsync(c => c.UserId == user.Id, cancellationToken),
                await _db.Connections.CountAsync(c => c.UserId == user.Id, cancellationToken),
                await _db.WebHooks.CountAsync(h => h.UserId == user.Id, cancellationToken),
                await workflows.CountAsync(cancellationToken),
                await workflows.CountAsync(w => w.Active, cancellationToken),
                user.CreatedAt, user.UpdatedAt);

            if (settings.Json)
            {
                Listing.WriteJsonDocument(details);
                return CliExit.Success;
            }

            var account = new Grid().AddColumn().AddColumn();
            void Row(string label, string value) => account.AddRow($"[grey]{label}[/]", Markup.Escape(value));
            Row("Id", details.Id.ToString());
            Row("User name", details.UserName ?? "");
            Row("Name", details.Name);
            Row("Email", details.Email is null ? "" : $"{details.Email}{(details.EmailConfirmed ? "" : " (unconfirmed)")}");
            Row("Time zone", details.TimeZone);
            Row("Roles", details.Roles.Count == 0 ? "none" : string.Join(", ", details.Roles));
            Row("2FA", details.TwoFactor ? "on" : "off");
            Row("Locked until", details.LockedUntil is null ? "not locked" : Listing.Time(details.LockedUntil));
            Row("Failed sign-ins", details.FailedSignIns.ToString());
            Row("Sessions", $"{details.Sessions}{(details.LastRefresh is { } last ? $", last refresh {Listing.Time(last)}" : "")}");
            Row("Owns", $"{details.Workflows} workflows ({details.ActiveWorkflows} active), {details.WebHooks} webhooks, " +
                        $"{details.Connections} connections, {details.ApiClients} API clients");
            Row("Created (UTC)", Listing.Time(details.CreatedAt));
            AnsiConsole.Write(account);

            if (details.Grants.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(GrantsTable(details.Grants));
            }
            return CliExit.Success;
        }

        public static Table GrantsTable(IEnumerable<UserGrantRow> grants)
        {
            var table = new Table().Title("Grants").AddColumns("Resource", "Actions", "Reason", "Expires (UTC)");
            foreach (var g in grants)
                table.AddRow(
                    g.Active ? Markup.Escape(g.Resource) : $"[grey]{Markup.Escape(g.Resource)} (expired)[/]",
                    Markup.Escape(string.Join(", ", g.Permissions)), Markup.Escape(g.Reason), Listing.Time(g.ExpiresAt));
            return table;
        }
    }
}
