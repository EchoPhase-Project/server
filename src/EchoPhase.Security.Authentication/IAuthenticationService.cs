// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Security.Authentication
{
    public interface IAuthenticationService
    {
        Task<IServiceResult> LoginAsync(string username, string password, CancellationToken ct = default);
        Task<IServiceResult> LogoutAsync(Guid userId, CancellationToken ct = default);
        Task<IServiceResult> LogoutAllAsync(Guid userId, CancellationToken ct = default);
        Task<IServiceResult> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    }
}
