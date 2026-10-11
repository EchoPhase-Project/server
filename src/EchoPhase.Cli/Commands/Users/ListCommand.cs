// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.Cli.Commands.Users
{
    public class ListSettings : CommandSettings
    {
    }

    /// <summary>Every user: name, roles, lockout, creation time.</summary>
    public class ListCommand : AsyncCommand<ListSettings>
    {
        private readonly UserRepository _users;
        private readonly IRoleService _roles;

        public ListCommand(UserRepository users, IRoleService roles)
        {
            _users = users;
            _roles = roles;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings, CancellationToken cancellationToken)
        {
            var users = await _users.Query().AsNoTracking().ToListAsync(cancellationToken);
            var table = new Table().AddColumns("User name", "Name", "Roles", "Locked until", "Created (UTC)");
            foreach (var user in users.OrderBy(u => u.UserName, StringComparer.Ordinal))
            {
                var roles = await _roles.GetRolesAsync(user);
                var locked = user.LockoutEnd > DateTimeOffset.UtcNow ? user.LockoutEnd.Value.ToString("u") : "";
                table.AddRow(
                    Markup.Escape(user.UserName ?? ""), Markup.Escape(user.Name),
                    Markup.Escape(string.Join(", ", roles)), locked, user.CreatedAt.ToString("u"));
            }
            AnsiConsole.Write(table);
            return CliExit.Success;
        }
    }
}
