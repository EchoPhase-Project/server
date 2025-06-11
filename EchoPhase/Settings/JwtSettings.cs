using EchoPhase.Extensions;
using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class JwtSettings : IValidatable
    {
        public string Secret { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ExpirationInMinutes
        {
            get; set;
        }

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Secret) || Secret.Length < 16)
            {
                errorMessage = "SecretKey is either empty or too short. It must be at least 16 characters long.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Issuer))
            {
                errorMessage = "Issuer is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Audience))
            {
                errorMessage = "Audience is required.";
                return false;
            }

            if (ExpirationInMinutes <= 0)
            {
                errorMessage = "ExpirationInMinutes must be greater than zero.";
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
