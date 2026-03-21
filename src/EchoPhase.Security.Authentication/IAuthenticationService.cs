// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

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
