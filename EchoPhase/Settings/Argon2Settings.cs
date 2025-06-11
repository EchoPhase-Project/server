using EchoPhase.Extensions;
using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class Argon2Settings : IValidatable
    {
        public string Secret { get; set; } = string.Empty;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Secret) || Secret.Length < 32)
            {
                errorMessage = "Key cannot be null, empty or less than 32 length.";
                return false;
            }

            if (!Secret.TryFromBase64String(out var _))
            {
                errorMessage = "Key must be base64";
                return false;
            }

            return true;
        }
    }
}
