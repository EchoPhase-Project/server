using System.ComponentModel;

namespace EchoPhase.Cli.Commands.Permissions.Deserialize
{
    public class DeserializeSettings : PermissionsSettings
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
