using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Security.Authentication.Jwt
{
    public interface IRefreshTokenProvider
    {
        Task<TokenPair> CreateAsync(User user, string deviceId);
        Task<TokenPair> RefreshAsync(string deviceId, string refreshToken);
        Task RevokeAsync(Guid userId, string deviceId, string refreshToken);
        Task RevokeAllAsync(Guid userId);
        Task<IEnumerable<RefreshToken>> GetSessionsAsync(Guid userId);
    }
}
