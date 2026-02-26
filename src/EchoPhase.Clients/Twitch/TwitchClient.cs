using EchoPhase.Clients.Twitch.Dto;
using EchoPhase.Clients.Twitch.Models;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Clients.Twitch
{
    public class TwitchClient : TwitchClientBase, ITwitchClient
    {
        private readonly HttpClient _client;
        private readonly ILogger<TwitchClient> _logger;

        public TwitchClient(
            HttpClient client, ILogger<TwitchClient> logger
        ) : base(client, logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<ITwitchApiResponse<IEnumerable<TwitchVipResponseDto>>> GetVipsAsync(
            TwitchVipRequestQueryDto query
        ) => await SendAsync<TwitchVipRequestQueryDto, object, List<TwitchVipResponseDto>>(
                "channels/vips",
                HttpMethod.Get,
                query,
                null
            );
    }
}
