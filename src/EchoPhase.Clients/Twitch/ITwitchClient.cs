using EchoPhase.Clients.Twitch.Dto;
using EchoPhase.Clients.Abstractions;

namespace EchoPhase.Clients.Twitch
{
    public interface ITwitchClient
    {
        public Task<PagedResult<IEnumerable<TwitchVipResponseDto>>> GetVipsAsync(
            TwitchVipRequestQueryDto query,
            string bearerToken,
            CancellationToken ct = default);
    }
}
