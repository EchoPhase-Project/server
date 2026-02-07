namespace EchoPhase.Configuration.Settings
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

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Key))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Key), "Key cannot be empty."));

            if (string.IsNullOrWhiteSpace(Issuer))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Issuer), "Issuer is required."));

            if (string.IsNullOrWhiteSpace(Audience))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Audience), "Audience is required."));

            if (string.IsNullOrWhiteSpace(ValidIssuer))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ValidIssuer), "ValidIssuer is required."));

            if (!ValidAudiences.Any(a => !string.IsNullOrWhiteSpace(a)))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ValidAudiences), "At least one valid audience is required."));

            if (LifetimeInMinutes <= 0)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(LifetimeInMinutes), "Lifetime must be greater than zero."));

            return ValidationResult.Success();
        }
    }
}
