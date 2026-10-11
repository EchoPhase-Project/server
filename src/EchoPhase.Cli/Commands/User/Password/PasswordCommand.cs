// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt.Providers;
using Microsoft.AspNetCore.Identity;
using UserModel = EchoPhase.DAL.Postgres.Models.User;

namespace EchoPhase.Cli.Commands.User.Password
{
    public class PasswordSettings : UserSettings
    {
    }

    /// <summary>
    /// Sets a new password (from standard input, or asked for) and ends every session
    /// of the user: the way back in for an administrator who forgot theirs.
    /// </summary>
    public class PasswordCommand : AsyncCommand<PasswordSettings>
    {
        private readonly UserRepository _users;
        private readonly UserManager<UserModel> _userManager;
        private readonly ISessionProvider _sessions;

        public PasswordCommand(UserRepository users, UserManager<UserModel> userManager, ISessionProvider sessions)
        {
            _users = users;
            _userManager = userManager;
            _sessions = sessions;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, PasswordSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            var password = UserLookup.ReadSecret("New password:");

            // Validated before the old password is removed, so a rejected one
            // does not leave the account without any.
            var problems = new List<IdentityError>();
            foreach (var validator in _userManager.PasswordValidators)
            {
                var check = await validator.ValidateAsync(_userManager, user, password);
                problems.AddRange(check.Errors);
            }
            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                    AnsiConsole.MarkupLineInterpolated($"[red]{problem.Description}[/]");
                return CliExit.Failure;
            }

            var removed = await _userManager.RemovePasswordAsync(user);
            var added = removed.Succeeded ? await _userManager.AddPasswordAsync(user, password) : removed;
            if (!added.Succeeded)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{string.Join(" ", added.Errors.Select(e => e.Description))}[/]");
                return CliExit.Failure;
            }

            await _sessions.RevokeAllAsync(user.Id, cancellationToken);
            await _userManager.ResetAccessFailedCountAsync(user);

            if (settings.Verbose)
                AnsiConsole.MarkupLineInterpolated($"[green]Password of '{settings.Username}' changed; their sessions are ended.[/]");
            return settings.Continue ? CliExit.StartServer : CliExit.Success;
        }
    }
}
