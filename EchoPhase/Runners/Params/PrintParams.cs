using System.Text.Json.Serialization;

using EchoPhase.Interfaces;

namespace EchoPhase.Runners.Params
{
    public class PrintParams : IValidatable
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("next")]
        public IEnumerable<int> Next { get; set; } = new HashSet<int>();

        public bool IsValid(out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(Text))
            {
                errorMessage = "Text cannot be empty.";
                return false;
            }

            return true;
        }
    }
}
