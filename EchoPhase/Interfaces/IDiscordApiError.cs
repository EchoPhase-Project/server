namespace EchoPhase.Interfaces
{
    public interface IDiscordApiError
    {
        int Code { get; set; }
        string? Message { get; set; }
        Dictionary<string, string[]>? Errors { get; set; }
    }
}
