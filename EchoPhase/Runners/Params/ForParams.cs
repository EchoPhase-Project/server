using System.Text.Json.Serialization;

using EchoPhase.Interfaces;

namespace EchoPhase.Runners.Params
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

        public bool IsValid(out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(CounterVar))
            {
                errorMessage = "CounterVar cannot be empty.";
                return false;
            }

            if (Step == 0)
            {
                errorMessage = "Step cannot be zero.";
                return false;
            }

            return true;
        }
    }
}
