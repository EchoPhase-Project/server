// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.Cli.Commands.Maintenance;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt.Providers;
using Microsoft.AspNetCore.Identity;
using UserModel = EchoPhase.DAL.Postgres.Models.User;

namespace EchoPhase.Cli.Commands.User.Lock
{
    public class LockSettings : UserSettings
    {
        [CommandOption("--for")]
        [Description("How long: 90m, 12h, 7d... (default: until unlocked)")]
        public string? For { get; set; }

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;
            if (For is not null && !Durations.TryParse(For, out _))
                return ValidationResult.Error("--for takes a duration such as 90m, 12h or 7d.");
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// Locks the account (until `user unlock`, or for a while) and ends its
    /// sessions, so it is out at once without being deleted.
    /// </summary>
    public class LockCommand : AsyncCommand<LockSettings>
    {
        private readonly UserRepository _users;
        private readonly UserManager<UserModel> _userManager;
        private readonly ISessionProvider _sessions;
        private readonly TimeProvider _time;

        public LockCommand(UserRepository users, UserManager<UserModel> userManager, ISessionProvider sessions, TimeProvider time)
        {
            _users = users;
            _userManager = userManager;
            _sessions = sessions;
            _time = time;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, LockSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            DateTimeOffset until = settings.For is not null && Durations.TryParse(settings.For, out var duration)
                ? _time.GetUtcNow() + duration
                : DateTimeOffset.MaxValue;

            // Identity ignores a lockout end while lockout is disabled for the user.
            var result = await _userManager.SetLockoutEnabledAsync(user, true);
            if (result.Succeeded)
                result = await _userManager.SetLockoutEndDateAsync(user, until);
            if (!result.Succeeded)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{string.Join(" ", result.Errors.Select(e => e.Description))}[/]");
                return CliExit.Failure;
            }

            await _sessions.RevokeAllAsync(user.Id, cancellationToken);
            await _userManager.UpdateSecurityStampAsync(user);

            if (settings.Verbose)
            {
                var span = until == DateTimeOffset.MaxValue ? "until unlocked" : $"until {until.UtcDateTime:u}";
                AnsiConsole.MarkupLineInterpolated($"[green]'{settings.Username}' is locked {span}; their sessions are ended.[/]");
            }
            return settings.Continue ? CliExit.StartServer : CliExit.Success;
        }
    }
}
