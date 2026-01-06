using EchoPhase.Commands;
using EchoPhase.Commands.Settings;
using EchoPhase.Registrars;
using Spectre.Console.Cli;

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
                config.ValidateExamples();

                config.AddBranch<PermissionsCommandSettings>("permissions", permissions =>
                {
                    permissions.SetDescription("Permission utilities");
                    permissions.SetDefaultCommand<PermissionsDeserializeCommand>();

                    permissions.AddCommand<PermissionsDeserializeCommand>("deserialize")
                        .WithDescription("Deserialize permissions")
                        .WithExample(new[] { "permissions", "deserialize", "0:2049;1:128" });
                });

                config.AddBranch<IntentsCommandSettings>("intents", permissions =>
                {
                    permissions.SetDescription("Intent utilities");
                    permissions.SetDefaultCommand<IntentsSerializeCommand>();

                    permissions.AddCommand<IntentsSerializeCommand>("serialize")
                        .WithDescription("Serialize intents")
                        .WithExample(new[] { "intents", "serialize", "login", "notification" });
                });

                config.AddBranch<UserCommandSettings>("user", user =>
                {
                    user.SetDescription("User management commands");
                    user.SetDefaultCommand<CreateUserCommand>();

                    user.AddBranch<RoleCommandSettings>("roles", roles =>
                    {
                        roles.SetDescription("Role management for users");
                        roles.SetDefaultCommand<AddToRolesCommand>();

                        roles.AddCommand<AddToRolesCommand>("add")
                            .WithDescription("Add roles to user")
                            .WithExample(new[] { "user", "Test", "roles", "add", "Dev", "User" });

                        roles.AddCommand<RemoveFromRolesCommand>("remove")
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

                config.AddCommand<HealthCheckCommand>("healthcheck")
                    .WithDescription("Healthcheck")
                    .WithExample(new[] { "healthcheck" });
            });

            return args.Count() > 0 ?  await app.RunAsync(args) : -1;
        }
    }
}
