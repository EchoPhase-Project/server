using EchoPhase.Dtos;

namespace EchoPhase.Interfaces
{
    public interface ITwitchClient
    {
        void WithAuth(string token);
        Task<ITwitchApiResponse<IEnumerable<TwitchVipResponseDto>>> GetVipsAsync(TwitchVipRequestQueryDto query);
    }
}
