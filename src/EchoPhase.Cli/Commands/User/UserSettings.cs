using System.ComponentModel;

namespace EchoPhase.Cli.Commands.User
{
    public class UserSettings : CommandSettings
    {
        [CommandArgument(0, "<USERNAME>")]
        [Description("User's username")]
        public string Username { get; set; } = string.Empty;

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
