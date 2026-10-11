// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.ApiClients;

namespace EchoPhase.Cli.Commands.User.ApiClients
{
    public class ApiClientsSettings : UserSettings
    {
    }

    public class ListSettings : ApiClientsSettings
    {
    }

    /// <summary>The user's API clients (never their secrets).</summary>
    public class ListCommand : AsyncCommand<ListSettings>
    {
        private readonly UserRepository _users;
        private readonly IApiClientService _clients;

        public ListCommand(UserRepository users, IApiClientService clients)
        {
            _users = users;
            _clients = clients;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            var table = new Table().AddColumns("Id", "Name", "Permissions", "Expires (UTC)", "Last used (UTC)");
            foreach (var client in await _clients.ListAsync(user.Id, cancellationToken))
                table.AddRow(
                    client.Id.ToString(), Markup.Escape(client.Name),
                    Markup.Escape(string.Join(" ", client.Permissions.Select(p => $"{p.Resource}:{string.Join(",", p.Permissions)}"))),
                    client.ExpiresAt?.ToString("u") ?? "never", client.LastUsedAt?.ToString("u") ?? "");
            AnsiConsole.Write(table);
            return CliExit.Success;
        }
    }

    public class CreateSettings : ApiClientsSettings
    {
        [CommandArgument(0, "<CLIENT_NAME>")]
        [Description("Name of the client, e.g. the bot it is for")]
        public string ClientName { get; set; } = string.Empty;

        [CommandOption("--permission|-p <RESOURCE:ACTIONS>")]
        [Description("Granted permissions, e.g. workflows:read,execute (repeatable); within the user's own")]
        public string[] Permissions { get; set; } = [];

        [CommandOption("--expires-in <DAYS>")]
        [Description("Days until the client stops working (default: never)")]
        public int? ExpiresInDays { get; set; }

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;
            if (string.IsNullOrWhiteSpace(ClientName))
                return ValidationResult.Error("Client name is required.");
            if (ExpiresInDays is <= 0)
                return ValidationResult.Error("--expires-in must be a positive number of days.");
            foreach (var permission in Permissions)
                if (!permission.Contains(':'))
                    return ValidationResult.Error($"'{permission}' is not RESOURCE:ACTIONS.");
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// Creates a client and prints its id and secret once: credentials for a bot or
    /// script set up without the web interface.
    /// </summary>
    public class CreateCommand : AsyncCommand<CreateSettings>
    {
        private readonly UserRepository _users;
        private readonly IApiClientService _clients;

        public CreateCommand(UserRepository users, IApiClientService clients)
        {
            _users = users;
            _clients = clients;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, CreateSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            var permissions = settings.Permissions
                .Select(p => p.Split(':', 2))
                .GroupBy(p => p[0].Trim(), StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(p => p[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                          .Distinct(StringComparer.Ordinal).ToArray(),
                    StringComparer.Ordinal);
            DateTimeOffset? expiresAt = settings.ExpiresInDays is { } days ? DateTimeOffset.UtcNow.AddDays(days) : null;

            var result = await _clients.CreateAsync(user.Id, settings.ClientName, new ApiClientGrant(permissions), expiresAt, cancellationToken);
            if (!result.Successful)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{result.Error.Message}[/]");
                return CliExit.Failure;
            }

            // Plain lines, easy to capture in a script; the secret is not shown again.
            Console.WriteLine($"client_id={result.Value!.Client.Id}");
            Console.WriteLine($"client_secret={result.Value.ClientSecret}");
            return settings.Continue ? CliExit.StartServer : CliExit.Success;
        }
    }

    public class DeleteSettings : ApiClientsSettings
    {
        [CommandArgument(0, "<CLIENT_ID>")]
        [Description("Id of the client (see 'apiclients list')")]
        public Guid ClientId { get; set; }
    }

    /// <summary>Deletes a client: its secret and tokens stop working.</summary>
    public class DeleteCommand : AsyncCommand<DeleteSettings>
    {
        private readonly UserRepository _users;
        private readonly IApiClientService _clients;

        public DeleteCommand(UserRepository users, IApiClientService clients)
        {
            _users = users;
            _clients = clients;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, DeleteSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            var result = await _clients.DeleteAsync(user.Id, settings.ClientId, cancellationToken);
            if (!result.Successful)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{result.Error.Message}[/]");
                return CliExit.Failure;
            }

            if (settings.Verbose)
                AnsiConsole.MarkupLineInterpolated($"[green]Client {settings.ClientId} deleted.[/]");
            return settings.Continue ? CliExit.StartServer : CliExit.Success;
        }
    }
}
