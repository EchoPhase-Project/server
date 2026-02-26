using System.ComponentModel;

namespace EchoPhase.Cli.Commands.Database.Migrate
{
    public class MigrateSettings : CommandSettings
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
