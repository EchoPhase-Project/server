using Spectre.Console.Cli;
using Spectre.Console;

namespace EchoPhase.Commands.Settings
{
    public class RolesCommandSettings : RoleCommandSettings
    {
        [CommandArgument(0, "<Roles>")]
        public string[] Roles { get; set; } = Array.Empty<string>();

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;

            if (Roles == null || Roles.Length == 0)
                return ValidationResult.Error("At least one tole is required.");

            foreach (var role in Roles)
                if (string.IsNullOrWhiteSpace(role))
                    return ValidationResult.Error("Roles cant be blank or whilespace.");

            return ValidationResult.Success();
        }
    }
}
