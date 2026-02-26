using System.ComponentModel;

namespace EchoPhase.Cli.Commands.User.Create
{
    public class CreateSettings : UserSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("User's name")]
        public string Name { get; set; } = "";

        [CommandArgument(1, "<PASSWORD>")]
        [Description("Base64 or plain text password")]
        public string Password { get; set; } = "";

        [CommandArgument(2, "[ROLES]")]
        [Description("List of roles to grant")]
        public string[] Roles { get; set; } = Array.Empty<string>();

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;

            if (string.IsNullOrWhiteSpace(Name))
                return ValidationResult.Error("Name is required.");

            if (string.IsNullOrWhiteSpace(Password))
                return ValidationResult.Error("Password is required.");

            return ValidationResult.Success();
        }
    }
}
