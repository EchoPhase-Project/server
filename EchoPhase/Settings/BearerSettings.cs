using EchoPhase.Interfaces;

namespace EchoPhase.Settings
{
    public class BearerSettings : IValidatable
    {
        public string Key { get; set; } = "jwt";
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string ValidIssuer { get; set; } = string.Empty;
        public IEnumerable<string> ValidAudiences { get; set; } = new HashSet<string>();
        public int LifetimeInMinutes
        {
            get; set;
        }

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Key))
            {
                errorMessage = "Key cannot be empty.";
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

            if (string.IsNullOrWhiteSpace(ValidIssuer))
            {
                errorMessage = "ValidIssuer is required.";
                return false;
            }

            if (!ValidAudiences.Any(a => !string.IsNullOrWhiteSpace(a)))
            {
                errorMessage = "At least one valid audience is required.";
                return false;
            }

            if (LifetimeInMinutes <= 0)
            {
                errorMessage = "Lifetime must be greater than zero.";
                return false;
            }

            return true;
        }
    }
}
