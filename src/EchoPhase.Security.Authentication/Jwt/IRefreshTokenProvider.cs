using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Types.Repository;

namespace EchoPhase.Security.Authentication.Jwt
{
    public interface IRefreshTokenProvider
    {
        Task<TokenPair> CreateAsync(User user, string deviceId);
        Task<TokenPair> RefreshAsync(string deviceId, string refreshToken);
        Task RevokeAsync(Guid userId, string deviceId, string refreshToken);
        Task RevokeAllAsync(Guid userId);
        Task<CursorPage<RefreshToken>> GetSessionsAsync(Guid userId, CursorOptions? cursor = null);
    }
}
