using System.Text.Json.Serialization;
using EchoPhase.Configuration.Settings;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    public class PrintParams : IValidatable
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("next")]
        public IEnumerable<int> Next { get; set; } = new HashSet<int>();

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Text))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Text), "Text cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
