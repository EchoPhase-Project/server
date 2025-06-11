using System.Security.Claims;
using EchoPhase.Models;
using EchoPhase.Repositories.Options;
using EchoPhase.Services;

namespace EchoPhase.Interfaces
{
    public interface IUserService : IDataService<UserService, UserOptions>
    {
        public IEnumerable<User> Get(
            EchoPhase.Repositories.Options.UserSearchOptions opts,
            Func<IQueryable<User>, EchoPhase.Repositories.Options.UserSearchOptions, IQueryable<User>>? extraFilters = null
        );
        public IEnumerable<User> Get(
            Action<EchoPhase.Repositories.Options.UserSearchOptions> configure,
            Func<IQueryable<User>, EchoPhase.Repositories.Options.UserSearchOptions, IQueryable<User>>? extraFilters = null
        );
        public Task<User> GetAsync(ClaimsPrincipal userPrincipal);
        public Task<string> GetOrSetCodeAsync(User user);
        public Task<IDictionary<Guid, string>> GetOrSetCodesAsync(params IEnumerable<User> users);
        public Task<Guid> GetIdFromCode(string code);
        public Task<IDictionary<string, Guid>> GetIdsFromCodes(params IEnumerable<string> codes);
        public bool UserExists(Guid id);
        public string GetProfileImagePath(Guid userId, string filename, bool root = false);
    }
}
