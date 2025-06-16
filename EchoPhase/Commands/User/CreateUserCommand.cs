using Spectre.Console.Cli;
using Spectre.Console;
using System.Text;
using EchoPhase.Commands.Settings;
using EchoPhase.Interfaces;
using EchoPhase.Extensions;

namespace EchoPhase.Commands
{
    public class CreateUserCommand : AsyncCommand<CreateUserCommandSettings>
    {
        private readonly IAuthService _authService;

        public CreateUserCommand(
            IAuthService authService
        )
        {
            _authService = authService;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, CreateUserCommandSettings settings)
        {
            var password = settings.Password;
            if (password.TryFromBase64String(out var bytes))
                password = Encoding.UTF8.GetString(bytes);

            var result = await _authService.CreateUserAsync(settings.Name, settings.Username, password, settings.Roles);

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
