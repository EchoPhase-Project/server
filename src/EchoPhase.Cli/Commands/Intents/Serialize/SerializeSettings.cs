namespace EchoPhase.Cli.Commands.Intents.Serialize
{
    public class SerializeSettings : IntentsSettings
    {
        [CommandArgument(0, "[INTENTS]")]
        public string[] Intents { get; set; } = Array.Empty<string>();

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;

            if (Intents is { Length: 0 })
                return ValidationResult.Error("At least one intent is required.");

            foreach (var intent in Intents)
                if (string.IsNullOrWhiteSpace(intent))
                    return ValidationResult.Error("Intents cant be blank or whilespace.");

            return ValidationResult.Success();
        }
    }
}
