// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.DAL.Redis.Maintenance;

namespace EchoPhase.Cli.Commands.Maintenance
{
    public class OnSettings : MaintenanceSettings
    {
        [CommandOption("--message|-m <TEXT>")]
        [Description("What users are told (default: a generic notice)")]
        public string? Message { get; set; }

        [CommandOption("--for <DURATION>")]
        [Description("End by itself after this long: 90s, 30m, 2h, 1d (default: until 'maintenance off')")]
        public string? For { get; set; }

        public override ValidationResult Validate()
        {
            if (For is not null && !Durations.TryParse(For, out _))
                return ValidationResult.Error($"'{For}' is not a duration; use e.g. 90s, 30m, 2h or 1d.");
            if (Message is { Length: > 500 })
                return ValidationResult.Error("The message is limited to 500 characters.");
            return ValidationResult.Success();
        }
    }

    /// <summary>Turns maintenance on for every server instance at once (within seconds), no deploy.</summary>
    public class OnCommand : AsyncCommand<OnSettings>
    {
        private readonly MaintenanceSwitch _maintenance;

        public OnCommand(MaintenanceSwitch maintenance)
        {
            _maintenance = maintenance;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, OnSettings settings, CancellationToken cancellationToken)
        {
            TimeSpan? duration = Durations.TryParse(settings.For, out var parsed) ? parsed : null;
            var state = await _maintenance.EnableAsync(settings.Message, duration, cancellationToken);
            AnsiConsole.MarkupLineInterpolated($"[yellow]Maintenance on[/] {(state.Until is { } until ? $"until {until:u}" : "until 'maintenance off'")}.");
            return CliExit.Success;
        }
    }
}
