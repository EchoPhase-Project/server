using EchoPhase.Commands.Settings;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Scylla;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands
{
    public class MigrationCommand : AsyncCommand<MigrationCommandSettings>
    {
        private readonly PostgresContext _pgContext;
        private readonly ScyllaContext _scyllaContext;

        public MigrationCommand(
            PostgresContext pgContext,
            ScyllaContext scyllaContext
        )
        {
            _pgContext = pgContext;
            _scyllaContext = scyllaContext;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, MigrationCommandSettings settings, CancellationToken cancellationToken)
        {
            var pgPendingMigrations = await _pgContext.Database.GetPendingMigrationsAsync();
            var scyllaPendingMigrations = await _scyllaContext.Database.GetPendingMigrationsAsync();

            if (pgPendingMigrations.Any())
            {
                await _pgContext.Database.MigrateAsync();
                if (settings.Verbose)
                    AnsiConsole.MarkupLine("[green]Postgres migrations were applied[/]");
            }

            if (scyllaPendingMigrations.Any())
            {
                await _scyllaContext.Database.MigrateAsync();
                if (settings.Verbose)
                    AnsiConsole.MarkupLine("[green]Scylla migrations were applied[/]");
            }

            return settings.Continue ? -1 : 0;
        }
    }
}
