// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Types.Service;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Identity
{
    public interface IUserService : IDataServiceBase<User, UserRepository>
    {
        Task<IdentityResult> CreateUserAsync(
            string name,
            string username,
            string password,
            params string[] roles);
        Task<IdentityResult> DeleteUserAsync(User user);
        Task<User?> GetAsync(ClaimsPrincipal userPrincipal);
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
