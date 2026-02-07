using EchoPhase.Clients.Twitch.Models;
using EchoPhase.Clients.Models;

namespace EchoPhase.Clients.Twitch.Extensions
{
    public static class TwitchClientExtensions
    {
        public static ITwitchApiResponse<T> ToTwitchApiResponse<T>(
            this IClientResponse<ITwitchApiResponseDto<T>, TwitchApiError> response
        ) where T : class => new TwitchApiResponse<T>(response);
    }
}
