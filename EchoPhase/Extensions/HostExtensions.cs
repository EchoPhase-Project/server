using Spectre.Console.Cli;
using EchoPhase.Commands;
using EchoPhase.Commands.Settings;
using EchoPhase.Registrars;

namespace EchoPhase.Extensions
{
    public static class HostExtensions
    {
        public static async Task<int> CheckArgsAsync(this IHost host, string[] args)
        {
            var registrar = new TypeRegistrar(host.Services);
            var app = new CommandApp(registrar);

            app.Configure(config =>
            {
                config.PropagateExceptions();

                config.AddBranch<UserCommandSettings>("user", user =>
                {
                    user.SetDescription("User management commands");

                    user.AddBranch<RoleCommandSettings>("roles", role =>
                    {
                        role.SetDescription("Role management for users");

                        role.AddCommand<AddToRolesCommand>("add")
                            .WithDescription("Add roles to user")
                            .WithExample(new[] { "user", "Test", "roles", "add", "Dev", "User" });

                        role.AddCommand<RemoveFromRolesCommand>("remove")
                            .WithDescription("Remove roles from user")
                            .WithExample(new[] { "user", "Test", "roles", "remove", "Dev", "User" });
                    });

                    user.AddCommand<CreateUserCommand>("create")
                        .WithDescription("Create user")
                        .WithExample(new[] { "user", "Test", "create", "Test", "Qwerty123456:/", "Dev", "User" });
                });

                config.AddCommand<MigrationCommand>("migrate")
                    .WithDescription("Apply database migrations")
                    .WithExample(new[] { "migrate" });
            });

            return await app.RunAsync(args);
        }
    }
}
