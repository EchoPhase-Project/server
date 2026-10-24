// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Identity;

namespace EchoPhase.Cli.Commands.User.Create
{
    public class CreateCommand : AsyncCommand<CreateSettings>
    {
        private readonly IUserService _userService;

        public CreateCommand(
                IUserService userService
        )
        {
            _userService = userService;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, CreateSettings settings, CancellationToken cancellationToken)
        {
            // Not an argument: it would show in the process list and the shell's
            // history. From stdin when redirected (scripts, kubectl exec -i).
            var password = UserLookup.ReadSecret("Password:");
            if (string.IsNullOrEmpty(password))
            {
                AnsiConsole.MarkupLine("[red]A password is required.[/]");
                return CliExit.Failure;
            }

            var result = await _userService.CreateUserAsync(settings.Name, settings.Username, password, timeZone: null, settings.Roles);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                AnsiConsole.MarkupLine($"[red]Failed to create user: {errors}[/]");
                return CliExit.Failure;
            }

            if (settings.Verbose)
                AnsiConsole.MarkupLine(
                    settings.Roles.Any()
                        ? $"[green]User '{settings.Username}' created with roles {string.Join(" ", settings.Roles.Select(r => $"'{r}'"))}[/]"
                        : $"[green]User '{settings.Username}' created[/]");

            return settings.Continue ? CliExit.StartServer : CliExit.Success;
        }
    }
}
