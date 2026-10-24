// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Redis.Maintenance;

namespace EchoPhase.Cli.Commands.Maintenance
{
    public class StatusCommand : AsyncCommand<MaintenanceSettings>
    {
        private readonly MaintenanceSwitch _maintenance;

        public StatusCommand(MaintenanceSwitch maintenance)
        {
            _maintenance = maintenance;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, MaintenanceSettings settings, CancellationToken cancellationToken)
        {
            if (await _maintenance.CurrentAsync(cancellationToken) is not { } state)
            {
                AnsiConsole.MarkupLine("[green]Maintenance off.[/]");
                return CliExit.Success;
            }

            AnsiConsole.MarkupLineInterpolated($"[yellow]Maintenance on[/] since {state.Since:u}, {(state.Until is { } until ? $"until {until:u}" : "until turned off")}.");
            if (state.Message is { } message)
                AnsiConsole.MarkupLineInterpolated($"Message: {message}");
            return CliExit.Success;
        }
    }
}
