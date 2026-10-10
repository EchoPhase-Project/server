// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.Authentication.Impersonation
{
    /// <summary>
    /// Hook for telling the target user; the host supplies the channel.
    /// </summary>
    public interface IImpersonationEvents
    {
        Task StartedAsync(Guid targetId, ImpersonationNotice notice, CancellationToken ct = default);
    }

    public class NullImpersonationEvents : IImpersonationEvents
    {
        public Task StartedAsync(Guid targetId, ImpersonationNotice notice, CancellationToken ct = default) => Task.CompletedTask;
    }
}
