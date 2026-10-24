// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.Cli.Commands.Maintenance;
using EchoPhase.Cli.Commands.User.Show;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;

namespace EchoPhase.Cli.Commands.User.Grants
{
    public class GrantsSettings : UserSettings
    {
    }

    public class ListSettings : GrantsSettings
    {
        [CommandOption("--json")]
        [Description("One JSON object per grant and line")]
        public bool Json { get; set; }
    }

    /// <summary>The user's grants, expired ones too (they explain a recent loss of access).</summary>
    public class ListCommand : AsyncCommand<ListSettings>
    {
        private readonly UserRepository _users;
        private readonly IUserPermissionService _permissions;
        private readonly TimeProvider _time;

        public ListCommand(UserRepository users, IUserPermissionService permissions, TimeProvider time)
        {
            _users = users;
            _permissions = permissions;
            _time = time;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            var now = _time.GetUtcNow().UtcDateTime;
            var grants = (await _permissions.DescribeAsync(user, cancellationToken)).Grants
                .Select(g => new UserGrantRow(g.Resource, g.Permissions, g.Reason, g.ExpiresAt, g.IsActive(now)))
                .ToList();

            if (settings.Json)
                grants.ForEach(Listing.WriteJson);
            else if (grants.Count == 0)
                AnsiConsole.MarkupLineInterpolated($"[grey]'{settings.Username}' has no grants.[/]");
            else
                AnsiConsole.Write(ShowCommand.GrantsTable(grants));
            return CliExit.Success;
        }
    }

    public class AddSettings : GrantsSettings
    {
        [CommandArgument(0, "<RESOURCE>")]
        [Description("Resource, e.g. workflows")]
        public string Resource { get; set; } = "";

        [CommandArgument(1, "<ACTIONS>")]
        [Description("Actions on it, e.g. read execute")]
        public string[] Actions { get; set; } = [];

        [CommandOption("--reason")]
        [Description("Why (required; shown to admins)")]
        public string Reason { get; set; } = "";

        [CommandOption("--for")]
        [Description("Expires after: 12h, 30d... (default: never)")]
        public string? For { get; set; }

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;
            if (string.IsNullOrWhiteSpace(Reason))
                return ValidationResult.Error("--reason is required.");
            if (For is not null && !Durations.TryParse(For, out _))
                return ValidationResult.Error("--for takes a duration such as 12h or 30d.");
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// Grants actions on one resource, replacing an earlier grant for it.
    /// Role-only actions (impersonation, granting, role assignment,
    /// maintenance) are refused: those come with a role.
    /// </summary>
    public class AddCommand : AsyncCommand<AddSettings>
    {
        private readonly UserRepository _users;
        private readonly IUserPermissionService _permissions;
        private readonly TimeProvider _time;

        public AddCommand(UserRepository users, IUserPermissionService permissions, TimeProvider time)
        {
            _users = users;
            _permissions = permissions;
            _time = time;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, AddSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            DateTimeOffset? expiresAt = settings.For is not null && Durations.TryParse(settings.For, out var duration)
                ? _time.GetUtcNow() + duration
                : null;

            // Accepts "read execute" as well as "read,execute".
            var actions = settings.Actions
                .SelectMany(a => a.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToArray();

            var result = await _permissions.GrantAsync(null, user, settings.Resource.Trim(), actions, settings.Reason, expiresAt, cancellationToken);
            if (!result.Successful)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{result.Error.Message}[/]");
                return CliExit.Failure;
            }

            if (settings.Verbose)
                AnsiConsole.MarkupLineInterpolated(
                    $"[green]'{settings.Username}' may now {string.Join(", ", result.Value!.Permissions)} on {result.Value.Resource}{(expiresAt is { } at ? $" until {at.UtcDateTime:u}" : "")}.[/]");
            return settings.Continue ? CliExit.StartServer : CliExit.Success;
        }
    }

    public class RemoveSettings : GrantsSettings
    {
        [CommandArgument(0, "<RESOURCE>")]
        [Description("Resource whose grant to remove")]
        public string Resource { get; set; } = "";
    }

    /// <summary>Removes the grant on a resource; the user's tokens are renewed without it.</summary>
    public class RemoveCommand : AsyncCommand<RemoveSettings>
    {
        private readonly UserRepository _users;
        private readonly IUserPermissionService _permissions;

        public RemoveCommand(UserRepository users, IUserPermissionService permissions)
        {
            _users = users;
            _permissions = permissions;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, RemoveSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            var result = await _permissions.RevokeAsync(user, settings.Resource.Trim(), cancellationToken);
            if (!result.Successful)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{result.Error.Message}[/]");
                return CliExit.Failure;
            }

            if (settings.Verbose)
                AnsiConsole.MarkupLineInterpolated($"[green]Grant on {settings.Resource} removed from '{settings.Username}'.[/]");
            return settings.Continue ? CliExit.StartServer : CliExit.Success;
        }
    }
}
