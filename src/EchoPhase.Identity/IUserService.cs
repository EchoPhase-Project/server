using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Repository;
using EchoPhase.Types.Service;

namespace EchoPhase.Identity
{
    public interface IUserService : IDataServiceBase<User, UserRepository, UserOptions>
    {
        CursorPage<User> Get(
            UserSearchOptions opts,
            CursorOptions? cursor = null,
            Func<IQueryable<User>, UserSearchOptions, IQueryable<User>>? extraFilters = null
        );
        CursorPage<User> Get(
            Action<UserSearchOptions> configure,
            Action<CursorOptions>? configureCursor = null,
            Func<IQueryable<User>, UserSearchOptions, IQueryable<User>>? extraFilters = null
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
