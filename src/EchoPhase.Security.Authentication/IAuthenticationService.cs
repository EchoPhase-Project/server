using EchoPhase.Types.Result;

namespace EchoPhase.Security.Authentication
{
    public interface IAuthenticationService
    {
        Task<IServiceResult> LoginAsync(string username, string password);
        Task<IServiceResult> LogoutAsync(Guid userId);
        Task<IServiceResult> LogoutAllAsync(Guid userId);
        Task<IServiceResult> RevokeSessionAsync(Guid userId, Guid tokenId);
    }
}
