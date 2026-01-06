using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands.Settings
{
    public class PermissionsDeserializeCommandSettings : PermissionsCommandSettings
    {
        [CommandArgument(0, "<PERMISSIONS>")]
        [Description("Serialized permissions")]
        public string Permissions { get; set; } = string.Empty;

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Permissions))
                return ValidationResult.Error("Permissions are required.");

            return ValidationResult.Success();
        }
    }
}
