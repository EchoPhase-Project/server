using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Types.Repository;

namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    public interface IRefreshTokenProvider
    {
        Task<TokenInitial> CreateAsync(
            User user,
            string deviceId,
            ClaimsEnrichmentContext? context = null);
        Task<TokenPair> RefreshAsync(Guid refreshId, string refreshToken);
        Task RevokeAsync(Guid userId, Guid refreshId);
        Task RevokeAllAsync(Guid userId);
        Task<CursorPage<RefreshToken>> GetSessionsAsync(Guid userId, CursorOptions? cursor = null);
    }
}
