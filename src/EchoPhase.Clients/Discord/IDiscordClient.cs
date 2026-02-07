using EchoPhase.Clients.Discord.Dto;
using EchoPhase.Clients.Discord.Models;

namespace EchoPhase.Clients.Discord
{
    public interface IDiscordClient
    {
        void WithAuth(string token);
        Task<IDiscordApiResponse<IEnumerable<DiscordUserGuildsResponseDto>>> GetUserGuildsAsync(DiscordUserGuildsQueryDto query);
    }
}
