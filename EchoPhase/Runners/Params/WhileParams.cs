using System.Text.Json.Serialization;

using EchoPhase.Interfaces;

namespace EchoPhase.Runners.Params
{
    public class WhileParams : IValidatable
    {
        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("bodyNext")]
        public IEnumerable<int> BodyNext { get; set; } = new HashSet<int>();

        [JsonPropertyName("afterNext")]
        public IEnumerable<int> AfterNext { get; set; } = new HashSet<int>();

        public bool IsValid(out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(Condition))
            {
                errorMessage = "Condition cannot be empty.";
                return false;
            }

            return true;
        }
    }
}
