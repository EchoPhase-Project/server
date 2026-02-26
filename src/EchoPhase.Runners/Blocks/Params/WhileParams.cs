using System.Text.Json.Serialization;
using EchoPhase.Configuration.Settings;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    public class WhileParams : IValidatable
    {
        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("bodyNext")]
        public IEnumerable<int> BodyNext { get; set; } = new HashSet<int>();

        [JsonPropertyName("afterNext")]
        public IEnumerable<int> AfterNext { get; set; } = new HashSet<int>();

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Condition))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Condition), "Condition cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
