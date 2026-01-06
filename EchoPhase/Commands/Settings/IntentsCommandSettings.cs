using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands.Settings
{
    public class IntentsCommandSettings : CommandSettings
    {
        public override ValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
