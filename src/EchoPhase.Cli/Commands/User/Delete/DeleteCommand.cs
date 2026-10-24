// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt.Providers;
using Microsoft.AspNetCore.Identity;
using RoleNames = EchoPhase.Security.BitMasks.Constants.Roles;
using UserModel = EchoPhase.DAL.Postgres.Models.User;

namespace EchoPhase.Cli.Commands.User.Delete
{
    public class DeleteSettings : UserSettings
    {
        [CommandOption("--confirm")]
        [Description("Delete without asking to type the user name (required when input is redirected)")]
        public bool Confirm { get; set; }
    }

    /// <summary>
    /// Deletes a user and everything they own (workflows, webhooks, connections,
    /// API clients, sessions...: the database cascades). Their sessions are
    /// ended first, so access tokens already issued stop working at once.
    /// </summary>
    public class DeleteCommand : AsyncCommand<DeleteSettings>
    {
        private readonly UserRepository _users;
        private readonly UserManager<UserModel> _userManager;
        private readonly ISessionProvider _sessions;

        public DeleteCommand(UserRepository users, UserManager<UserModel> userManager, ISessionProvider sessions)
        {
            _users = users;
            _userManager = userManager;
            _sessions = sessions;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, DeleteSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            var admins = await _userManager.GetUsersInRoleAsync(RoleNames.Admin);
            if (UserDeletion.IsLastAdmin(user.Id, admins.Select(a => a.Id)))
            {
                AnsiConsole.MarkupLineInterpolated($"[red]'{settings.Username}' is the only admin; make someone else admin first.[/]");
                return CliExit.Failure;
            }

            if (!settings.Confirm)
            {
                if (Console.IsInputRedirected)
                {
                    AnsiConsole.MarkupLine("[red]Input is redirected: pass --confirm to delete without asking.[/]");
                    return CliExit.Failure;
                }

                var typed = AnsiConsole.Prompt(new TextPrompt<string>(
                    $"Delete '{Markup.Escape(user.UserName ?? settings.Username)}' and everything they own? Type the user name to confirm:")
                    .AllowEmpty());
                if (!UserDeletion.Confirms(typed, user.UserName ?? settings.Username))
                {
                    AnsiConsole.MarkupLine("[yellow]Not deleted.[/]");
                    return CliExit.Failure;
                }
            }

            await _sessions.RevokeAllAsync(user.Id, cancellationToken);
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{string.Join(" ", result.Errors.Select(e => e.Description))}[/]");
                return CliExit.Failure;
            }

            if (settings.Verbose)
                AnsiConsole.MarkupLineInterpolated($"[green]'{settings.Username}' deleted.[/]");
            return settings.Continue ? CliExit.StartServer : CliExit.Success;
        }
    }

    public static class UserDeletion
    {
        /// <summary>Whether the user is an admin and no one else is.</summary>
        public static bool IsLastAdmin(Guid userId, IEnumerable<Guid> adminIds)
        {
            var admins = adminIds.ToHashSet();
            return admins.Contains(userId) && admins.Count == 1;
        }

        /// <summary>The typed confirmation is the user name, exactly (surrounding spaces aside).</summary>
        public static bool Confirms(string? typed, string username)
            => string.Equals(typed?.Trim(), username, StringComparison.Ordinal);
    }
}
