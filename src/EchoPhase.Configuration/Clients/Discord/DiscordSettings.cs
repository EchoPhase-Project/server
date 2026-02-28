using System.Text.RegularExpressions;

namespace EchoPhase.Configuration.Clients.Discord
{
    public partial class DiscordSettings : IValidatable
    {
        public string Token { get; set; } = string.Empty;

        [GeneratedRegex(@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$")]
        private static partial Regex TokenRegex();

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Token))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Token), "Token cannot be null or empty."));

            if (!TokenRegex().IsMatch(Token))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Token), "Token format is invalid."));

            return ValidationResult.Success();
        }
    }
}
