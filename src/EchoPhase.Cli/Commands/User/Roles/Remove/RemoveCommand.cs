using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;

namespace EchoPhase.Cli.Commands.User.Roles.Remove
{
    public class RemoveCommand : AsyncCommand<RemoveSettings>
    {
        private readonly UserRepository _userRepository;
        private readonly IRoleService _roleService;

        public RemoveCommand(
            UserRepository userRepository,
            IRoleService roleService
        )
        {
            _userRepository = userRepository;
            _roleService = roleService;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, RemoveSettings settings, CancellationToken cancellationToken)
        {
            var users = _userRepository.Query()
                .WithUserNames(settings.Username)
                .ToHashSet();

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
