// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Server.Registrars;
using Spectre.Console;
using Spectre.Console.Cli;
using Commands = EchoPhase.Cli.Commands;
using DiagnosticsCommands = EchoPhase.Server.Services.Diagnostics;
using WorkflowCommands = EchoPhase.Server.Services.Workflows.Commands;

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
                    // A bare `user <name>` only looks.
                    user.SetDefaultCommand<Commands.User.Show.ShowCommand>();

                    user.AddCommand<Commands.User.Show.ShowCommand>("show")
                        .WithDescription("Account state, roles, grants and what the user owns")
                        .WithExample(new[] { "user", "admin", "show" })
                        .WithExample(new[] { "user", "admin", "show", "--json" });

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
                        .WithDescription("Create a user; the password from stdin, or asked for")
                        .WithExample(new[] { "user", "Test", "create", "Test", "dev" });

                    user.AddCommand<Commands.User.Password.PasswordCommand>("password")
                        .WithDescription("Set a new password (from stdin, or asked for) and end the user's sessions")
                        .WithExample(new[] { "user", "admin", "password" });

                    user.AddCommand<Commands.User.Unlock.UnlockCommand>("unlock")
                        .WithDescription("Lift a lockout after failed sign-ins")
                        .WithExample(new[] { "user", "admin", "unlock" });

                    user.AddCommand<Commands.User.Lock.LockCommand>("lock")
                        .WithDescription("Lock the account (until unlocked, or --for a while) and end its sessions")
                        .WithExample(new[] { "user", "probe1234", "lock" })
                        .WithExample(new[] { "user", "probe1234", "lock", "--for", "7d" });

                    user.AddBranch<Commands.User.Grants.GrantsSettings>("grants", grants =>
                    {
                        grants.SetDescription("Permissions given to the user beyond their roles");
                        grants.SetDefaultCommand<Commands.User.Grants.ListCommand>();

                        grants.AddCommand<Commands.User.Grants.ListCommand>("list")
                            .WithDescription("List the user's grants, expired ones too")
                            .WithExample(new[] { "user", "admin", "grants", "list" });

                        grants.AddCommand<Commands.User.Grants.AddCommand>("add")
                            .WithDescription("Grant actions on a resource (replaces an earlier grant on it)")
                            .WithExample(new[] { "user", "Test", "grants", "add", "roles", "read", "--reason", "Support rotation", "--for", "30d" });

                        grants.AddCommand<Commands.User.Grants.RemoveCommand>("remove")
                            .WithDescription("Remove the grant on a resource")
                            .WithExample(new[] { "user", "Test", "grants", "remove", "roles" });
                    });

                    user.AddCommand<Commands.User.Delete.DeleteCommand>("delete")
                        .WithDescription("Delete the user and everything they own, ending their sessions")
                        .WithExample(new[] { "user", "probe1234", "delete" })
                        .WithExample(new[] { "user", "probe1234", "delete", "--confirm" });

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
                    .WithDescription("List users with their roles and lockouts, a page at a time")
                    .WithExample(new[] { "users" })
                    .WithExample(new[] { "users", "--search", "probe", "--role", "admin" })
                    .WithExample(new[] { "users", "--json", "--all" });

                config.AddBranch("workflows", workflows =>
                {
                    workflows.SetDescription("Workflows of all users");
                    workflows.SetDefaultCommand<Commands.Workflows.ListCommand>();

                    workflows.AddCommand<Commands.Workflows.ListCommand>("list")
                        .WithDescription("List workflows, a page at a time, with their next scheduled run")
                        .WithExample(new[] { "workflows", "list", "--user", "admin", "--active" });

                    workflows.AddCommand<WorkflowCommands.ImportCommand>("import")
                        .WithDescription("Create an inactive workflow from `workflow export` output")
                        .WithExample(new[] { "workflows", "import", "backup.json", "--user", "admin" });
                });

                config.AddBranch<Commands.Workflows.WorkflowSettings>("workflow", workflow =>
                {
                    workflow.SetDescription("One workflow, by id");

                    workflow.AddCommand<Commands.Workflows.ExportCommand>("export")
                        .WithDescription("Print the workflow as JSON ({ name, program })")
                        .WithExample(new[] { "workflow", "0199a0c4-7d2e-7c4b-9f00-000000000000", "export" });

                    workflow.AddCommand<WorkflowCommands.EnableCommand>("enable")
                        .WithDescription("Turn its triggers on (the server applies it within a minute)")
                        .WithExample(new[] { "workflow", "0199a0c4-7d2e-7c4b-9f00-000000000000", "enable" });

                    workflow.AddCommand<WorkflowCommands.DisableCommand>("disable")
                        .WithDescription("Turn its triggers off (the server applies it within a minute)")
                        .WithExample(new[] { "workflow", "0199a0c4-7d2e-7c4b-9f00-000000000000", "disable" });
                });

                config.AddCommand<Commands.Impersonations.ListCommand>("impersonations")
                    .WithDescription("Who acted as whom and why, newest first")
                    .WithExample(new[] { "impersonations", "--user", "admin" })
                    .WithExample(new[] { "impersonations", "--active" });

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

                config.AddBranch<Commands.Storage.StorageSettings>("storage", storage =>
                {
                    storage.SetDescription("Object storage (S3): check it, upload, download, list and remove objects");

                    storage.AddCommand<Commands.Storage.CheckCommand>("check")
                        .WithDescription("Check that the bucket answers")
                        .WithExample(new[] { "storage", "check" });

                    storage.AddCommand<Commands.Storage.PutCommand>("put")
                        .WithDescription("Upload a file (stdin when no file is given) under a key")
                        .WithExample(new[] { "storage", "put", "tests/hello.txt", "hello.txt" })
                        .WithExample(new[] { "storage", "put", "tests/notes.md", "notes.md", "--content-type", "text/markdown" });

                    storage.AddCommand<Commands.Storage.GetCommand>("get")
                        .WithDescription("Download an object to a file or stdout")
                        .WithExample(new[] { "storage", "get", "tests/hello.txt" })
                        .WithExample(new[] { "storage", "get", "tests/hello.txt", "hello.txt" });

                    storage.AddCommand<Commands.Storage.ListCommand>("ls")
                        .WithDescription("List objects, optionally under a prefix")
                        .WithExample(new[] { "storage", "ls" })
                        .WithExample(new[] { "storage", "ls", "tests/", "--json" });

                    storage.AddCommand<Commands.Storage.DeleteCommand>("rm")
                        .WithDescription("Remove an object")
                        .WithExample(new[] { "storage", "rm", "tests/hello.txt" });
                });

                config.AddCommand<Commands.Database.Migrate.MigrateCommand>("migrate")
                    .WithDescription("Apply database migrations")
                    .WithExample(new[] { "migrate" })
                    .WithExample(new[] { "migrate", "--status" });

                config.AddCommand<Commands.Prune.PruneCommand>("prune")
                    .WithDescription("Delete idle sessions, old rotated tokens and long-ended impersonations and grants")
                    .WithExample(new[] { "prune", "--dry-run" })
                    .WithExample(new[] { "prune", "--older-than", "30d" });

                config.AddCommand<Commands.Check.CheckCommand>("check")
                    .WithDescription("Validate the settings and check Postgres, Scylla, Redis, object storage (if configured) and pending migrations")
                    .WithExample(new[] { "check" });

                config.AddCommand<DiagnosticsCommands.ReportTestErrorCommand>("report-test-error")
                    .WithDescription("Send a test error to GlitchTip (SENTRY_DSN) and wait until it is delivered")
                    .WithExample(new[] { "report-test-error" });

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
