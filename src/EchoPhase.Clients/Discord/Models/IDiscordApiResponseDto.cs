namespace EchoPhase.Clients.Discord.Models
{
    public interface IDiscordApiResponseDto<T>
    {
        T? Data
        {
            get; set;
        }
        string? Message
        {
            get; set;
        }
    }
}
