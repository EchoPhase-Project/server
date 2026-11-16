// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.Authentication.Jwt.Providers
{
    /// <summary>
    /// Hook for what must stop when sessions end (sign-out, revoke, refresh
    /// token reuse); the host closes their realtime connections. Called after
    /// the sessions are removed. It must not throw: the sessions are gone already.
    /// </summary>
    public interface ISessionEvents
    {
        Task EndedAsync(Guid userId, IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default);
    }

    public class NullSessionEvents : ISessionEvents
    {
        public Task EndedAsync(Guid userId, IReadOnlyCollection<Guid> sessionIds, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
