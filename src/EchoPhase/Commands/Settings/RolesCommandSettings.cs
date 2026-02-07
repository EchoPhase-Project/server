using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands.Settings
{
    public class RolesCommandSettings : RoleCommandSettings
    {
        [CommandArgument(0, "[ROLES]")]
        public string[] Roles { get; set; } = Array.Empty<string>();

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;

            if (Roles == null || Roles.Length == 0)
                return ValidationResult.Error("At least one role is required.");

            foreach (var role in Roles)
                if (string.IsNullOrWhiteSpace(role))
                    return ValidationResult.Error("Roles cant be blank or whilespace.");

            return ValidationResult.Success();
        }
    }
}
