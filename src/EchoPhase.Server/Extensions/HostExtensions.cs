// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Server.Registrars;
using Spectre.Console;
using Spectre.Console.Cli;
using Commands = EchoPhase.Cli.Commands;

namespace EchoPhase.Server.Extensions
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

                    user.AddCommand<Commands.User.Password.PasswordCommand>("password")
                        .WithDescription("Set a new password (from stdin, or asked for) and end the user's sessions")
                        .WithExample(new[] { "user", "admin", "password" });

                    user.AddCommand<Commands.User.Unlock.UnlockCommand>("unlock")
                        .WithDescription("Lift a lockout after failed sign-ins")
                        .WithExample(new[] { "user", "admin", "unlock" });

                    user.AddCommand<Commands.User.Sessions.SessionsCommand>("sessions")
                        .WithDescription("List the user's sessions, or end them all")
                        .WithExample(new[] { "user", "admin", "sessions", "--revoke-all" });

                    user.AddBranch<Commands.User.ApiClients.ApiClientsSettings>("apiclients", clients =>
                    {
                        clients.SetDescription("API clients (credentials for bots and scripts) of the user");
                        clients.SetDefaultCommand<Commands.User.ApiClients.ListCommand>();

                        clients.AddCommand<Commands.User.ApiClients.ListCommand>("list")
                            .WithDescription("List the user's API clients")
                            .WithExample(new[] { "user", "admin", "apiclients", "list" });

                        clients.AddCommand<Commands.User.ApiClients.CreateCommand>("create")
                            .WithDescription("Create a client; prints its id and secret once")
                            .WithExample(new[] { "user", "admin", "apiclients", "create", "discord-bot", "-p", "workflows:read,execute", "--expires-in", "90" });

                        clients.AddCommand<Commands.User.ApiClients.DeleteCommand>("delete")
                            .WithDescription("Delete a client")
                            .WithExample(new[] { "user", "admin", "apiclients", "delete", "0199a0c4-7d2e-7c4b-9f00-000000000000" });
                    });
                });

                config.AddCommand<Commands.Users.ListCommand>("users")
                    .WithDescription("List users with their roles and lockouts")
                    .WithExample(new[] { "users" });

                config.AddCommand<Commands.Roles.ListCommand>("roles")
                    .WithDescription("List the roles and what each grants")
                    .WithExample(new[] { "roles" });

                config.AddBranch<Commands.Maintenance.MaintenanceSettings>("maintenance", maintenance =>
                {
                    maintenance.SetDescription("Maintenance mode for the whole deployment, without a deploy");
                    maintenance.SetDefaultCommand<Commands.Maintenance.StatusCommand>();

                    maintenance.AddCommand<Commands.Maintenance.OnCommand>("on")
                        .WithDescription("Turn maintenance on: the API answers 503 except for admins")
                        .WithExample(new[] { "maintenance", "on" })
                        .WithExample(new[] { "maintenance", "on", "--message", "Database upgrade", "--for", "30m" });

                    maintenance.AddCommand<Commands.Maintenance.OffCommand>("off")
                        .WithDescription("Turn maintenance off")
                        .WithExample(new[] { "maintenance", "off" });

                    maintenance.AddCommand<Commands.Maintenance.StatusCommand>("status")
                        .WithDescription("Show whether maintenance is on")
                        .WithExample(new[] { "maintenance", "status" });
                });

                config.AddCommand<Commands.Database.Migrate.MigrateCommand>("migrate")
                    .WithDescription("Apply database migrations")
                    .WithExample(new[] { "migrate" })
                    .WithExample(new[] { "migrate", "--status" });

                config.AddCommand<Commands.Health.Check.CheckCommand>("healthcheck")
                    .WithDescription("Healthcheck")
                    .WithExample(new[] { "healthcheck" })
                    .WithExample(new[] { "healthcheck", "--url", "http://localhost:8080/health/ready" });
            });

            if (args.Length == 0)
                return Commands.CliExit.StartServer;

            try
            {
                return await app.RunAsync(args);
            }
            catch (CommandAppException ex)
            {
                // A mistyped command or option: say so instead of crashing.
                AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
                return Commands.CliExit.Failure;
            }
        }
    }
}
