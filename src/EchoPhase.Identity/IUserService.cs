using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Types.Repository;
using EchoPhase.Types.Service;
using Microsoft.AspNetCore.Identity;
using RepositoryUserOptions = EchoPhase.DAL.Postgres.Repositories.Options.UserOptions;

namespace EchoPhase.Identity
{
    public interface IUserService : IDataServiceBase<User, UserRepository, RepositoryUserOptions>
    {
        Task<IdentityResult> CreateUserAsync(
            string name,
            string username,
            string password,
            params string[] roles);
        Task<IdentityResult> DeleteUserAsync(User user);
        Task<User> GetAsync(ClaimsPrincipal userPrincipal);
        Task<string> GetOrSetCodeAsync(User user);
        Task<IDictionary<Guid, string>> GetOrSetCodesAsync(params IEnumerable<User> users);
        Task<Guid> GetIdFromCode(string code);
        Task<IDictionary<string, Guid>> GetIdsFromCodes(params IEnumerable<string> codes);
        bool UserExists(Guid id);
        bool UserExists(string username);
        string GetProfileImagePath(Guid userId, string filename, bool root = false);
        Task<IdentityResult> UpdateSecurityStampAsync(User user);
    }
}
