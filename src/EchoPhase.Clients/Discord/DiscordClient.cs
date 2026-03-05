using EchoPhase.Clients.Discord.Dto;
using EchoPhase.Clients.Abstractions;

namespace EchoPhase.Clients.Discord
{
    public class DiscordClient : DiscordClientBase, IDiscordClient
    {
        public DiscordClient(HttpClient client) : base(client) { }

        public async Task<Result<IEnumerable<DiscordUserGuildsResponseDto>>> GetUserGuildsAsync(
            DiscordUserGuildsQueryDto query,
            string botToken,
            CancellationToken ct = default)
        {
            var result = await SendAsync<DiscordUserGuildsQueryDto, object, List<DiscordUserGuildsResponseDto>>(
                "users/@me/guilds", HttpMethod.Get, query, null, botToken, ct);

            return result.Map(list => list.AsEnumerable());
        }
    }
}
