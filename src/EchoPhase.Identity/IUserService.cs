// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.DAL.Abstractions.Service;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Identity
{
    public interface IUserService : IDataServiceBase<User, UserRepository>
    {
        Task<IdentityResult> CreateUserAsync(
            string name,
            string username,
            string password,
            string? timeZone = null,
            params string[] roles);
        /// <summary>
        /// Changes the given profile fields; null leaves a field as it is.
        /// </summary>
        Task<IdentityResult> UpdateProfileAsync(User user, string? name, string? timeZone);
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
