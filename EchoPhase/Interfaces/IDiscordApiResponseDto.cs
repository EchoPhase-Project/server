namespace EchoPhase.Interfaces
{
    public interface IDiscordApiResponseDto<T>
    {
        T? Data { get; set; }
        string? Message { get; set; }
    }
}
