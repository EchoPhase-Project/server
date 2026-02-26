using Commands = EchoPhase.Cli.Commands;
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

                config.AddBranch<Commands.Permissions.PermissionsSettings>("permissions", permissions =>
                {
                    permissions.SetDescription("Permission utilities");
                    permissions.SetDefaultCommand<Commands.Permissions.Deserialize.DeserializeCommand>();

                    permissions.AddCommand<Commands.Permissions.Deserialize.DeserializeCommand>("deserialize")
                        .WithDescription("Deserialize permissions")
                        .WithExample(new[] { "permissions", "deserialize", "0:2049;1:128" });
                });

                config.AddBranch<Commands.Intents.IntentsSettings>("intents", permissions =>
                {
                    permissions.SetDescription("Intent utilities");
                    permissions.SetDefaultCommand<Commands.Intents.Serialize.SerializeCommand>();

                    permissions.AddCommand<Commands.Intents.Serialize.SerializeCommand>("serialize")
                        .WithDescription("Serialize intents")
                        .WithExample(new[] { "intents", "serialize", "login", "notification" });
                });

                config.AddBranch<Commands.User.UserSettings>("user", user =>
                {
                    user.SetDescription("User management commands");
                    user.SetDefaultCommand<Commands.User.Create.CreateCommand>();

                    user.AddBranch<Commands.User.Roles.RolesSettings>("roles", roles =>
                    {
                        roles.SetDescription("Role management for users");
                        roles.SetDefaultCommand<Commands.User.Roles.Add.AddCommand>();

                        roles.AddCommand<Commands.User.Roles.Add.AddCommand>("add")
                            .WithDescription("Add roles to user")
                            .WithExample(new[] { "user", "Test", "roles", "add", "Dev", "User" });

                        roles.AddCommand<Commands.User.Roles.Remove.RemoveCommand>("remove")
                            .WithDescription("Remove roles from user")
                            .WithExample(new[] { "user", "Test", "roles", "remove", "Dev", "User" });
                    });

                    user.AddCommand<Commands.User.Create.CreateCommand>("create")
                        .WithDescription("Create user")
                        .WithExample(new[] { "user", "Test", "create", "Test", "Qwerty123456:/", "Dev", "User" });
                });

                config.AddCommand<Commands.Database.Migrate.MigrateCommand>("migrate")
                    .WithDescription("Apply database migrations")
                    .WithExample(new[] { "migrate" });

                config.AddCommand<Commands.Health.Check.CheckCommand>("healthcheck")
                    .WithDescription("Healthcheck")
                    .WithExample(new[] { "healthcheck" });
#if DEBUG
                config.PropagateExceptions();
                config.ValidateExamples();
#endif
            });

            return args.Count() > 0 ? await app.RunAsync(args) : -1;
        }
    }
}
