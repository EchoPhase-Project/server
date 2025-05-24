namespace EchoPhase.Interfaces
{
    public interface IDiscordApiResponse<out T> : IClientResponse<T, IDiscordApiError>
    {
    }
}
