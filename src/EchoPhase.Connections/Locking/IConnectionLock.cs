// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Connections.Locking
{
    /// <summary>
    /// Serialises token refreshes for one connection across all app instances.
    /// </summary>
    public interface IConnectionLock
    {
        Task<IAsyncDisposable> AcquireAsync(Guid userId, string provider, string name, CancellationToken ct = default);
    }
}
