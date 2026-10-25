// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Entities;

namespace EchoPhase.Notifications
{
    /// <summary>Where users' notifications are kept: one inbox per user, newest first.</summary>
    public interface INotificationStore
    {
        /// <summary>Writes a notification (new or changed) that expires after <paramref name="ttl"/>.</summary>
        Task SaveAsync(Notification notification, TimeSpan ttl, CancellationToken ct = default);

        /// <summary>The user's newest notifications, at most <paramref name="limit"/>.</summary>
        Task<IReadOnlyList<Notification>> ListAsync(Guid userId, int limit, CancellationToken ct = default);

        Task<Notification?> FindAsync(Guid userId, Guid id, CancellationToken ct = default);

        Task DeleteAsync(Notification notification, CancellationToken ct = default);
    }
}
