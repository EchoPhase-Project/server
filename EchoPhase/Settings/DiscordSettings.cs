using System.Text.RegularExpressions;

using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class DiscordSettings : IValidatable
    {
        public string? Token { get; set; } = string.Empty;

        private static readonly Regex TokenRegex = new Regex(@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$", RegexOptions.Compiled);

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Token))
            {
                errorMessage = "Token cannot be null or empty.";
                return false;
            }

            if (!TokenRegex.IsMatch(Token))
            {
                errorMessage = "Token format is invalid.";
                return false;
            }

            return true;
        }
    }
}
