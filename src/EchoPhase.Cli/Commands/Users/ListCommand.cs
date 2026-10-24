// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.DAL.Postgres;
using Microsoft.EntityFrameworkCore;
using UserModel = EchoPhase.DAL.Postgres.Models.User;

namespace EchoPhase.Cli.Commands.Users
{
    public class ListSettings : ListingSettings
    {
        [CommandOption("--search|-s")]
        [Description("Only users whose user name, name or email contains this (any case)")]
        public string? Search { get; set; }

        [CommandOption("--role|-r")]
        [Description("Only users with this role")]
        public string? Role { get; set; }

        [CommandOption("--locked")]
        [Description("Only users locked out now")]
        public bool Locked { get; set; }
    }

    /// <summary>A record of one user in a listing.</summary>
    public sealed record UserRow(
        Guid Id, string? UserName, string Name, string? Email, IReadOnlyList<string> Roles,
        DateTimeOffset? LockedUntil, bool TwoFactor, DateTime CreatedAt);

    /// <summary>
    /// Users by user name, a page at a time, with their roles (one query per
    /// page, not per user). The table says how many match in all.
    /// </summary>
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
            var now = _time.GetUtcNow();
            var filtered = Filter(settings, now);
            var table = new Table().AddColumns("User name", "Name", "Roles", "Locked until", "2FA", "Created (UTC)");

            var next = await Listing.PagesAsync(settings,
                (after, take, ct) =>
                {
                    var page = filtered;
                    if (after is not null)
                        page = page.Where(u => string.Compare(u.UserName, after) > 0);
                    return page.OrderBy(u => u.UserName).Take(take).ToListAsync(ct);
                },
                u => u.UserName ?? "",
                async users =>
                {
                    foreach (var row in await WithRolesAsync(users, now, cancellationToken))
                    {
                        if (settings.Json)
                            Listing.WriteJson(row);
                        else
                            table.AddRow(
                                Markup.Escape(row.UserName ?? ""), Markup.Escape(row.Name),
                                Markup.Escape(string.Join(", ", row.Roles)), Listing.Time(row.LockedUntil),
                                row.TwoFactor ? "yes" : "", Listing.Time(row.CreatedAt));
                    }
                },
                cancellationToken);

            if (!settings.Json)
            {
                var total = await filtered.CountAsync(cancellationToken);
                table.Caption($"{table.Rows.Count} shown of {total}");
                AnsiConsole.Write(table);
            }
            Listing.Footer(settings, next);
            return CliExit.Success;
        }

        private IQueryable<UserModel> Filter(ListSettings settings, DateTimeOffset now)
        {
            var users = _db.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(settings.Search))
            {
                var pattern = Listing.Contains(settings.Search.Trim());
                users = users.Where(u =>
                    EF.Functions.ILike(u.UserName!, pattern) ||
                    EF.Functions.ILike(u.Name, pattern) ||
                    EF.Functions.ILike(u.Email!, pattern));
            }
            if (!string.IsNullOrWhiteSpace(settings.Role))
            {
                var role = settings.Role.Trim();
                users = users.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id &&
                    _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));
            }
            if (settings.Locked)
                users = users.Where(u => u.LockoutEnd > now);
            return users;
        }

        private async Task<List<UserRow>> WithRolesAsync(IReadOnlyList<UserModel> users, DateTimeOffset now, CancellationToken ct)
        {
            var ids = users.Select(u => u.Id).ToList();
            var roles = (await (
                    from ur in _db.UserRoles
                    join r in _db.Roles on ur.RoleId equals r.Id
                    where ids.Contains(ur.UserId)
                    select new { ur.UserId, r.Name })
                .ToListAsync(ct))
                .ToLookup(x => x.UserId, x => x.Name ?? "");

            return users.Select(u => new UserRow(
                    u.Id, u.UserName, u.Name, u.Email,
                    roles[u.Id].Order(StringComparer.Ordinal).ToList(),
                    u.LockoutEnd > now ? u.LockoutEnd : null,
                    u.TwoFactorEnabled, u.CreatedAt))
                .ToList();
        }
    }
}
