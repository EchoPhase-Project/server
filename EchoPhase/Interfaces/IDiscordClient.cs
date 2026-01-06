using EchoPhase.Dtos;

namespace EchoPhase.Interfaces
{
    public interface IDiscordClient
    {
        void WithAuth(string token);
        Task<IDiscordApiResponse<IEnumerable<DiscordUserGuildsResponseDto>>> GetUserGuildsAsync(DiscordUserGuildsQueryDto query);
    }
}
