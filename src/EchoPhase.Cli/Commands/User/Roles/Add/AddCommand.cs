using EchoPhase.Identity;

namespace EchoPhase.Cli.Commands.User.Roles.Add
{
    public class AddCommand : AsyncCommand<AddSettings>
    {
        private readonly IUserService _userService;
        private readonly IRoleService _roleService;

        public AddCommand(
            IUserService userService,
            IRoleService roleService
        )
        {
            _roleService = roleService;
            _userService = userService;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, AddSettings settings, CancellationToken cancellationToken)
        {
            var users = _userService.Get(opts =>
            {
                opts.UserNames = new HashSet<string> { settings.Username };
            }).Data.ToHashSet();

            if (!users.Any())
            {
                AnsiConsole.MarkupLine($"[red]No users found for username '{settings.Username}'[/]");
                return -1;
            }

            foreach (var user in users)
            {
                await _roleService.AddToRolesAsync(user, settings.Roles);
                if (settings.Verbose)
                    AnsiConsole.MarkupLine($"[green]User '{settings.Username}' granted role(s) {string.Join(" ", settings.Roles.Select(r => $"'{r}'"))}[/]");
            }

            return settings.Continue ? 0 : 1;
        }
    }
}
