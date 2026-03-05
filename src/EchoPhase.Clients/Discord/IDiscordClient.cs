using EchoPhase.Clients.Discord.Dto;
using EchoPhase.Clients.Abstractions;

namespace EchoPhase.Clients.Discord
{
    public interface IDiscordClient
    {
        Task<Result<IEnumerable<DiscordUserGuildsResponseDto>>> GetUserGuildsAsync(
            DiscordUserGuildsQueryDto query,
            string botToken,
            CancellationToken ct = default);
    }
}
