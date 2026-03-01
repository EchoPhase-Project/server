namespace EchoPhase.Configuration.Authentication.Bearer
{
    public class BearerOptions : IValidatable
    {
        private const int MinLifetimeMinutes = 5;

        public const string SectionName = "Authentication:Bearer";
        public string Key { get; set; } = "jwt";
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string ValidIssuer { get; set; } = string.Empty;
        public IEnumerable<string> ValidAudiences { get; set; } = new HashSet<string>();
        public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(525600);

        public IValidationResult Validate()
        {
            if (Lifetime.TotalMinutes < MinLifetimeMinutes)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Lifetime),
                        $"Lifetime must be at least {MinLifetimeMinutes} minutes."));

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

            return ValidationResult.Success();
        }
    }
}
