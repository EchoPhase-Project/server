// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Repositories;
using Microsoft.AspNetCore.Identity;
using UserModel = EchoPhase.DAL.Postgres.Models.User;

namespace EchoPhase.Cli.Commands.User.Unlock
{
    public class UnlockSettings : UserSettings
    {
    }

    /// <summary>Lifts a lockout after failed sign-ins and resets the failure count.</summary>
    public class UnlockCommand : AsyncCommand<UnlockSettings>
    {
        private readonly UserRepository _users;
        private readonly UserManager<UserModel> _userManager;

        public UnlockCommand(UserRepository users, UserManager<UserModel> userManager)
        {
            _users = users;
            _userManager = userManager;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, UnlockSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            var result = await _userManager.SetLockoutEndDateAsync(user, null);
            if (result.Succeeded)
                result = await _userManager.ResetAccessFailedCountAsync(user);
            if (!result.Succeeded)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{string.Join(" ", result.Errors.Select(e => e.Description))}[/]");
                return CliExit.Failure;
            }

            if (settings.Verbose)
                AnsiConsole.MarkupLineInterpolated($"[green]'{settings.Username}' can sign in again.[/]");
            return settings.Continue ? CliExit.StartServer : CliExit.Success;
        }
    }
}
