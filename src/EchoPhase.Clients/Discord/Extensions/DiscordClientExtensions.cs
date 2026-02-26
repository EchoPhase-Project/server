using EchoPhase.Clients.Discord.Models;
using EchoPhase.Clients.Models;

namespace EchoPhase.Clients.Discord.Extensions
{
    public static class DiscordClientExtensions
    {
        public static IDiscordApiResponse<T> ToDiscordApiResponse<T>(
            this IClientResponse<T, IDiscordApiError> response
        ) where T : class => new DiscordApiResponse<T>(response);
    }
}
