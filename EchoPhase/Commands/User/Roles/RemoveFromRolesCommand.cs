using EchoPhase.Commands.Settings;
using EchoPhase.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands
{
    public class RemoveFromRolesCommand : AsyncCommand<RolesCommandSettings>
    {
        private readonly IUserService _userService;
        private readonly IRoleService _roleService;

        public RemoveFromRolesCommand(
            IUserService userService,
            IRoleService roleService
        )
        {
            _roleService = roleService;
            _userService = userService;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, RolesCommandSettings settings, CancellationToken cancellationToken)
        {
            var users = _userService.Get(opts =>
            {
                opts.UserNames = new HashSet<string> { settings.Username };
            }).ToHashSet();

            if (users is { Count: 0 })
            {
                AnsiConsole.MarkupLine($"[red]No users found for username '{settings.Username}'[/]");
                return -1;
            }

            foreach (var user in users)
            {
                await _roleService.RemoveFromRolesAsync(user, settings.Roles);
                if (settings.Verbose)
                    AnsiConsole.MarkupLine($"[green]User '{settings.Username}' revoked role(s) {string.Join(" ", settings.Roles.Select(r => $"'{r}'"))}[/]");
            }

            return settings.Continue ? 0 : 1;
        }
    }
}
