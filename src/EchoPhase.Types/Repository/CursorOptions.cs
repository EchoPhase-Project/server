namespace EchoPhase.Types.Repository
{
    public class CursorOptions
    {
        public string? After { get; set; } = null;
        public int Limit { get; set; } = 20;
    }
}
