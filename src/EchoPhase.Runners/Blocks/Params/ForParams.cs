using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    public class ForParams : IValidatable
    {
        [JsonPropertyName("counterVar")]
        public string CounterVar { get; set; } = string.Empty;

        [JsonPropertyName("start")]
        public int Start
        {
            get; set;
        }

        [JsonPropertyName("end")]
        public int End
        {
            get; set;
        }

        [JsonPropertyName("step")]
        public int Step { get; set; } = 1;

        [JsonPropertyName("bodyNext")]
        public IEnumerable<int> BodyNext { get; set; } = new HashSet<int>();

        [JsonPropertyName("afterNext")]
        public IEnumerable<int> AfterNext { get; set; } = new HashSet<int>();

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(CounterVar))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(CounterVar), "CounterVar cannot be empty."));

            if (Step == 0)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Step), "Step cannot be zero."));

            return ValidationResult.Success();
        }
    }
}
