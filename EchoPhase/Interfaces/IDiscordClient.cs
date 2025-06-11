using EchoPhase.Dtos;

namespace EchoPhase.Interfaces
{
    public interface IDiscordClient
    {
        public Task<IDiscordApiResponse<IEnumerable<DiscordUserGuildsResponseDto>>> GetUserGuildsAsync(DiscordUserGuildsQueryDto query);
    }
}
