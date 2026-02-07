using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands.Settings
{
    public class HealthCheckCommandSettings : CommandSettings
    {
        [CommandOption("--verbose|-v")]
        [DefaultValue(false)]
        [Description("Show command output")]
        public bool Verbose { get; set; } = false;

        public override ValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
