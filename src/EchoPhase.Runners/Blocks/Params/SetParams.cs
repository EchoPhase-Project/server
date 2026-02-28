using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Types.Validation;
using Newtonsoft.Json.Linq;

namespace EchoPhase.Runners.Blocks.Params
{
    public class SetParams : IValidatable
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public JToken Value { get; set; } = JValue.CreateNull();

        [JsonPropertyName("next")]
        public IEnumerable<int> Next { get; set; } = new HashSet<int>();

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Name), "Name cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
