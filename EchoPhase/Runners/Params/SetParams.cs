using System.Text.Json.Serialization;
using EchoPhase.Interfaces;
using Newtonsoft.Json.Linq;

namespace EchoPhase.Runners.Params
{
    public class SetParams : IValidatable
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public JToken Value { get; set; } = JValue.CreateNull();

        [JsonPropertyName("next")]
        public IEnumerable<int> Next { get; set; } = new HashSet<int>();

        public bool IsValid(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                errorMessage = "Name cannot be empty.";
                return false;
            }
            errorMessage = "";
            return true;
        }
    }
}
