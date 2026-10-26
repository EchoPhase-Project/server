// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Security.Authentication
{
    public interface IAuthenticationService
    {
        Task<IServiceResult> LoginAsync(string username, string password, CancellationToken ct = default);
        Task<IServiceResult> LogoutAsync(Guid userId, CancellationToken ct = default);
        Task<IServiceResult> LogoutAllAsync(Guid userId, CancellationToken ct = default);
        Task<IServiceResult> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

        /// <summary>
        /// Changes the password (the current one must be right) and signs out
        /// every other device; this request's device stays signed in.
        /// </summary>
        Task<IdentityResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword,
            CancellationToken ct = default);
    }
}
