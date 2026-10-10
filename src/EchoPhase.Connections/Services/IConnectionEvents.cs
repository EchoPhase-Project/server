// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections.Models;

namespace EchoPhase.Connections.Services
{
    /// <summary>
    /// Hook for telling the user about connection state changes. The host
    /// application supplies the delivery channel.
    /// </summary>
    public interface IConnectionEvents
    {
        Task ReauthRequiredAsync(Guid userId, ConnectionSummary connection, CancellationToken ct = default);
    }

    public class NullConnectionEvents : IConnectionEvents
    {
        public Task ReauthRequiredAsync(Guid userId, ConnectionSummary connection, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
