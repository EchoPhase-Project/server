namespace EchoPhase.Configuration.Authentication.Refresh
{
    public class RefreshOptions : IValidatable
    {
        private const int MinLifetimeMinutes = 5;

        public const string SectionName = "Authentication:Refresh";
        public string Key { get; set; } = "refresh";
        public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(120);

        public IValidationResult Validate()
        {
            if (Lifetime.TotalMinutes < MinLifetimeMinutes)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Lifetime),
                        $"Lifetime must be at least {MinLifetimeMinutes} minutes."));

            return ValidationResult.Success();
        }
    }
}
