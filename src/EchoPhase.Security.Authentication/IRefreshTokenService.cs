using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Security.Authentication
{
    public interface IRefreshTokenService
    {
        Task<TokenPair> CreateAsync(User user, string deviceId);
        Task<TokenPair> RefreshAsync(string deviceId, string refreshToken);
        Task RevokeAsync(string deviceId, string refreshToken);
        Task RevokeAllAsync(Guid userId);
    }
}
