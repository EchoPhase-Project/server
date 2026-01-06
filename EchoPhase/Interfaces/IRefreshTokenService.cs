using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Models;

namespace EchoPhase.Interfaces
{
    public interface IRefreshTokenService
    {
        Task<TokenPair> CreateAsync(User user, string deviceId);
        Task<TokenPair> RefreshAsync(string deviceId, string refreshToken);
        Task RevokeAsync(string deviceId, string refreshToken);
        Task RevokeAllAsync(Guid userId);
    }
}
