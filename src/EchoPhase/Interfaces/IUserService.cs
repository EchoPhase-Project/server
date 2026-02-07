using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;

namespace EchoPhase.Interfaces
{
    public interface IUserService : IDataServiceBase<UserRepository>
    {
        IEnumerable<User> Get(
            EchoPhase.DAL.Postgres.Repositories.Options.UserSearchOptions opts,
            Func<IQueryable<User>, EchoPhase.DAL.Postgres.Repositories.Options.UserSearchOptions, IQueryable<User>>? extraFilters = null
        );
        IEnumerable<User> Get(
            Action<EchoPhase.DAL.Postgres.Repositories.Options.UserSearchOptions> configure,
            Func<IQueryable<User>, EchoPhase.DAL.Postgres.Repositories.Options.UserSearchOptions, IQueryable<User>>? extraFilters = null
        );
        Task<User> GetAsync(ClaimsPrincipal userPrincipal);
        Task<string> GetOrSetCodeAsync(User user);
        Task<IDictionary<Guid, string>> GetOrSetCodesAsync(params IEnumerable<User> users);
        Task<Guid> GetIdFromCode(string code);
        Task<IDictionary<string, Guid>> GetIdsFromCodes(params IEnumerable<string> codes);
        bool UserExists(Guid id);
        bool UserExists(string username);
        string GetProfileImagePath(Guid userId, string filename, bool root = false);
    }
}
