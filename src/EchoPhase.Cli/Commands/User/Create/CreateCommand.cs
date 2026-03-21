// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using EchoPhase.Identity;
using EchoPhase.Types.Extensions;

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
            var password = settings.Password;
            if (password.TryFromBase64String(out var bytes))
                password = Encoding.UTF8.GetString(bytes);

            var result = await _userService.CreateUserAsync(settings.Name, settings.Username, password, settings.Roles);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                AnsiConsole.MarkupLine($"[red]Failed to create user: {errors}[/]");
                return -1;
            }

            if (settings.Verbose)
                AnsiConsole.MarkupLine(
                    settings.Roles.Any()
                        ? $"[green]User '{settings.Username}' created with roles {string.Join(" ", settings.Roles.Select(r => $"'{r}'"))}[/]"
                        : $"[green]User '{settings.Username}' created[/]");

            return settings.Continue ? 0 : 1;
        }
    }
}
