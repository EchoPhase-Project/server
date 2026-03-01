using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Clients
{
    public class ClientsOptions : IValidatable
    {
        public const string SectionName = "Clients";
        public Discord.DiscordOptions Discord { get; set; } = new();

        public IValidationResult Validate()
        {
            return Discord.Validate().WithPrefix(nameof(Discord))
                .Then(() => ValidateSelf());
        }

        private IValidationResult ValidateSelf()
        {
            return ValidationResult.Success();
        }
    }
}
