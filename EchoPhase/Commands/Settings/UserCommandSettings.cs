using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;

namespace EchoPhase.Commands.Settings
{
    public class UserCommandSettings : CommandSettings
    {
        [CommandArgument(0, "<USERNAME>")]
        [Description("User's username")]
        public string Username { get; set; } = "";

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
            if (string.IsNullOrWhiteSpace(Username))
                return ValidationResult.Error("Username is required.");

            return ValidationResult.Success();
        }
    }
}
