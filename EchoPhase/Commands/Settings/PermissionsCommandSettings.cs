using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands.Settings
{
    public class PermissionsCommandSettings : CommandSettings
    {
        public override ValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
