// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Redis.Maintenance
{
    /// <summary>Where the maintenance state lives: shared by every server instance and the CLI.</summary>
    public interface IMaintenanceStore
    {
        Task<MaintenanceState?> GetAsync(CancellationToken ct = default);

        /// <summary>Turns it on; with <paramref name="expiry"/>, it turns itself off after that long.</summary>
        Task SetAsync(MaintenanceState state, TimeSpan? expiry, CancellationToken ct = default);

        Task ClearAsync(CancellationToken ct = default);
    }
}
