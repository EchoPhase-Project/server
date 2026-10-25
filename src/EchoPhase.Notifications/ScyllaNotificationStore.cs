// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.DAL.Scylla.Linq;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.Notifications
{
    /// <summary>Notifications in ScyllaDB: a partition per user, rows expiring by TTL.</summary>
    public class ScyllaNotificationStore : INotificationStore
    {
        private readonly ScyllaContext _db;

        public ScyllaNotificationStore(ScyllaContext db)
        {
            _db = db;
        }

        public async Task SaveAsync(Notification notification, TimeSpan ttl, CancellationToken ct = default)
        {
            _db.Notifications.Add(notification, new WriteOptions(Ttl: ttl));
            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<Notification>> ListAsync(Guid userId, int limit, CancellationToken ct = default)
        {
            var page = await _db.Notifications
                .Where(n => n.UserId == userId)
                .ToPageAsync(limit, null, ct);
            return page.Items;
        }

        public Task<Notification?> FindAsync(Guid userId, Guid id, CancellationToken ct = default)
            => _db.Notifications
                .Where(n => n.UserId == userId && n.Id == id)
                .FirstOrDefaultAsync(ct);

        public async Task DeleteAsync(Notification notification, CancellationToken ct = default)
        {
            _db.Notifications.Remove(notification);
            await _db.SaveChangesAsync(ct);
        }
    }
}
