using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Types.Result;

namespace EchoPhase.Clients
{
    public interface IExternalTokenService
    {
        Task<IServiceResult<byte[]>> GetAsync(Guid userId, string providerName, string tokenName);
        Task<int> SetAsync(ExternalToken entity);
        Task<bool> DeleteAsync(Guid userId, string providerName, string tokenName);
        Task DeleteAllAsync(Guid userId);
        IEnumerable<string> GetKeyNames(Guid userId);
    }
}
