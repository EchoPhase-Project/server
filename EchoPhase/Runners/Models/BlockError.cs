namespace EchoPhase.Runners.Models
{
    public class BlockError
    {
        public int Id
        {
            get; set;
        }
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
