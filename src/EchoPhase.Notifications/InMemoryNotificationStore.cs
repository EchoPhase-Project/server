// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using EchoPhase.DAL.Scylla.Entities;

namespace EchoPhase.Notifications
{
    /// <summary>Notifications in memory, for tests and a server without ScyllaDB: expiry included.</summary>
    public class InMemoryNotificationStore : INotificationStore
    {
        private readonly ConcurrentDictionary<(Guid User, Guid Id), (Notification Value, DateTimeOffset Expires)> _items = new();
        private readonly TimeProvider _time;

        public InMemoryNotificationStore(TimeProvider time)
        {
            _time = time;
        }

        public Task SaveAsync(Notification notification, TimeSpan ttl, CancellationToken ct = default)
        {
            _items[(notification.UserId, notification.Id)] = (Copy(notification), _time.GetUtcNow() + ttl);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Notification>> ListAsync(Guid userId, int limit, CancellationToken ct = default)
        {
            var now = _time.GetUtcNow();
            IReadOnlyList<Notification> items = _items
                .Where(e => e.Key.User == userId && e.Value.Expires > now)
                .Select(e => Copy(e.Value.Value))
                // UUIDv7 ids sort by time, as the table's clustering order does.
                .OrderByDescending(n => n.Id.ToString("N"), StringComparer.Ordinal)
                .Take(limit)
                .ToList();
            return Task.FromResult(items);
        }

        public Task<Notification?> FindAsync(Guid userId, Guid id, CancellationToken ct = default)
            => Task.FromResult(_items.TryGetValue((userId, id), out var entry) && entry.Expires > _time.GetUtcNow()
                ? Copy(entry.Value)
                : null);

        public Task DeleteAsync(Notification notification, CancellationToken ct = default)
        {
            _items.TryRemove((notification.UserId, notification.Id), out _);
            return Task.CompletedTask;
        }

        private static Notification Copy(Notification n) => new()
        {
            UserId = n.UserId,
            Id = n.Id,
            Kind = n.Kind,
            Title = n.Title,
            Body = n.Body,
            Link = n.Link,
            Params = n.Params,
            Read = n.Read,
            CreatedAt = n.CreatedAt,
        };
    }
}
