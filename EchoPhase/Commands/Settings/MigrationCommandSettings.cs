using Spectre.Console.Cli;
using System.ComponentModel;
using Spectre.Console;

namespace EchoPhase.Commands.Settings
{
    public class MigrationCommandSettings : CommandSettings
    {
        [CommandOption("--continue|-c")]
        [DefaultValue(false)]
        [Description("Continue execution after migration")]
        public bool Continue { get; set; } = false;

        [CommandOption("--verbose|-v")]
        [DefaultValue(true)]
        [Description("Show command output")]
        public bool Verbose { get; set; } = true;

        public override ValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
