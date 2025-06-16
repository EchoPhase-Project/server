using Spectre.Console.Cli;
using Spectre.Console;
using EchoPhase.Commands.Settings;
using EchoPhase.DAL.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.Commands
{
    public class MigrationCommand : AsyncCommand<MigrationCommandSettings>
    {
        private readonly PostgresContext _context;

        public MigrationCommand(PostgresContext context)
        {
            _context = context;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, MigrationCommandSettings settings)
        {
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                await _context.Database.MigrateAsync();
                if (settings.Verbose)
                    AnsiConsole.MarkupLine("[green]Migrations were applied[/]");
            }
            else
            {
                if (settings.Verbose)
                    AnsiConsole.MarkupLine("[green]All migrations already applied[/]");
            }

            return settings.Continue ? 0 : 1;
        }
    }
}
