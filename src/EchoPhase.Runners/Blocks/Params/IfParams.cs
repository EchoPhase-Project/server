using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    public class IfParams : IValidatable
    {
        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("trueNext")]
        public IEnumerable<int> TrueNext { get; set; } = new HashSet<int>();

        [JsonPropertyName("falseNext")]
        public IEnumerable<int> FalseNext { get; set; } = new HashSet<int>();

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Condition))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Condition), "Condition cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
