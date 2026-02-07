using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands.Settings
{
    public class MigrationCommandSettings : CommandSettings
    {
        [CommandOption("--continue|-c")]
        [DefaultValue(false)]
        [Description("Continue execution after migration")]
        public bool Continue { get; set; } = false;

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
