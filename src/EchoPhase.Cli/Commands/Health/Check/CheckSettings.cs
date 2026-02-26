using System.ComponentModel;

namespace EchoPhase.Cli.Commands.Health.Check
{
    public class CheckSettings : CommandSettings
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
