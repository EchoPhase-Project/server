using EchoPhase.Clients.Twitch.Dto;
using EchoPhase.Clients.Twitch.Models;

namespace EchoPhase.Clients.Twitch
{
    public interface ITwitchClient
    {
        void WithAuth(string token);
        Task<ITwitchApiResponse<IEnumerable<TwitchVipResponseDto>>> GetVipsAsync(TwitchVipRequestQueryDto query);
    }
}
