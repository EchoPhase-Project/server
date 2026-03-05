using EchoPhase.Clients.Twitch.Dto;
using EchoPhase.Clients.Abstractions;

namespace EchoPhase.Clients.Twitch
{
    public class TwitchClient : TwitchClientBase, ITwitchClient
    {
        public TwitchClient(HttpClient client) : base(client) { }

        public Task<PagedResult<IEnumerable<TwitchVipResponseDto>>> GetVipsAsync(
            TwitchVipRequestQueryDto query,
            string bearerToken,
            CancellationToken ct = default)
        => SendAsync<TwitchVipRequestQueryDto, object, TwitchVipResponseDto>(
                "channels/vips", HttpMethod.Get, query, null, bearerToken, ct);
    }
}
