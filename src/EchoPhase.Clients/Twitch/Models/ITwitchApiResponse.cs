using EchoPhase.Clients.Models;

namespace EchoPhase.Clients.Twitch.Models
{
    public interface ITwitchApiResponse<out T> : IClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError>
    {
    }
}
