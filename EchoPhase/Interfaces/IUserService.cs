using System.Security.Claims;
using EchoPhase.Models;
using EchoPhase.Repositories.Options;
using EchoPhase.Services;

namespace EchoPhase.Interfaces
{
    public interface IUserService : IDataService<UserService, UserOptions>
    {
        IEnumerable<User> Get(
            EchoPhase.Repositories.Options.UserSearchOptions opts,
            Func<IQueryable<User>, EchoPhase.Repositories.Options.UserSearchOptions, IQueryable<User>>? extraFilters = null
        );
        IEnumerable<User> Get(
            Action<EchoPhase.Repositories.Options.UserSearchOptions> configure,
            Func<IQueryable<User>, EchoPhase.Repositories.Options.UserSearchOptions, IQueryable<User>>? extraFilters = null
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
