// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Redis.Maintenance;

namespace EchoPhase.Cli.Commands.Maintenance
{
    public class OffCommand : AsyncCommand<MaintenanceSettings>
    {
        private readonly MaintenanceSwitch _maintenance;

        public OffCommand(MaintenanceSwitch maintenance)
        {
            _maintenance = maintenance;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, MaintenanceSettings settings, CancellationToken cancellationToken)
        {
            await _maintenance.DisableAsync(cancellationToken);
            AnsiConsole.MarkupLine("[green]Maintenance off.[/]");
            return CliExit.Success;
        }
    }
}
