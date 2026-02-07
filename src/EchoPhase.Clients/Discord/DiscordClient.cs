using EchoPhase.Clients.Discord.Dto;
using Microsoft.Extensions.Logging;
using EchoPhase.Clients.Discord.Models;
using EchoPhase.Clients.Models;

namespace EchoPhase.Clients.Discord
{
    public class DiscordClient : DiscordClientBase, IDiscordClient
    {
        private readonly HttpClient _client;
        private readonly ILogger<DiscordClient> _logger;

        public DiscordClient(
            HttpClient client, ILogger<DiscordClient> logger
        ) : base(client, logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<IDiscordApiResponse<IEnumerable<DiscordUserGuildsResponseDto>>> GetUserGuildsAsync(
            DiscordUserGuildsQueryDto query
        ) => await SendAsync<DiscordUserGuildsQueryDto, object, List<DiscordUserGuildsResponseDto>>(
                "users/@me/guilds",
                HttpMethod.Get,
                query,
                null
            );
    }
}
