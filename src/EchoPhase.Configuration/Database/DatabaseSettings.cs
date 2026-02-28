using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Database
{
    public class DatabaseSettings : IValidatable
    {
        public Redis.RedisSettings Redis { get; set; }= new();
        public Postgres.PostgresSettings Postgres { get; set; } = new();
        public Scylla.ScyllaSettings Scylla { get; set; } = new();

        public IValidationResult Validate()
        {
            return Redis.Validate().WithPrefix(nameof(Redis))
                .Then(() => Postgres.Validate().WithPrefix(nameof(Postgres)))
                .Then(() => Scylla.Validate().WithPrefix(nameof(Scylla)))
                .Then(() => ValidateSelf());
        }

        private IValidationResult ValidateSelf()
        {
            return ValidationResult.Success();
        }
    }
}
