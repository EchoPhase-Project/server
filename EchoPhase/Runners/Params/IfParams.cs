using System.Text.Json.Serialization;

using EchoPhase.Interfaces;

namespace EchoPhase.Runners.Params
{
    public class IfParams : IValidatable
    {
        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("trueNext")]
        public IEnumerable<int> TrueNext { get; set; } = new HashSet<int>();

        [JsonPropertyName("falseNext")]
        public IEnumerable<int> FalseNext { get; set; } = new HashSet<int>();

        public bool IsValid(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(Condition))
            {
                errorMessage = "Condition cannot be empty.";
                return false;
            }
            errorMessage = "";
            return true;
        }
    }
}
