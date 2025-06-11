using EchoPhase.Dtos;
using EchoPhase.Interfaces;

namespace EchoPhase.Clients
{
    public class TwitchClient : TwitchClientBase
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

        //public async Task<TwitchUserResponseDto> GetUsersAsync(TwitchUserRequestDto dto) =>
        //	await SendRequestAsync<TwitchUserRequestDto, TwitchUserResponseDto>("users", HttpMethod.Get, dto);

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
