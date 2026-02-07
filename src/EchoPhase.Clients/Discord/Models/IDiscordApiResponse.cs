using EchoPhase.Clients.Models;

namespace EchoPhase.Clients.Discord.Models
{
    public interface IDiscordApiResponse<out T> : IClientResponse<T, IDiscordApiError>
    {
    }
}
