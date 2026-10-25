// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;
using System.Text.Json;
using EchoPhase.Configuration.Runner;
using EchoPhase.Configuration.Runner.Blocks;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Configurations;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Runners.Blocks.Storage;
using EchoPhase.Scripting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Services.Storage
{
    /// <summary>
    /// Workflow storage in Postgres (StorageEntries). A write takes a
    /// transaction-scoped advisory lock on its key first, so concurrent
    /// updates of one key run one after another, a missing key included
    /// (there is no row to lock yet). Expired values count as missing, also
    /// for the quota, until `prune` deletes them.
    /// </summary>
    public sealed class WorkflowStore : IWorkflowStore
    {
        private readonly PostgresContext _db;
        private readonly BlocksStorageOptions _options;
        private readonly TimeProvider _time;

        public WorkflowStore(PostgresContext db, IOptions<RunnerOptions> options, TimeProvider time)
        {
            _db = db;
            _options = options.Value.Blocks.Storage;
            _time = time;
        }

        private DateTime Now => _time.GetUtcNow().UtcDateTime;

        public async Task<StorageItem?> GetAsync(StorageSlot slot, string key, CancellationToken ct)
        {
            var now = Now;
            var entry = await In(slot).AsNoTracking()
                .Where(e => e.Key == key && (e.ExpiresAt == null || e.ExpiresAt > now))
                .FirstOrDefaultAsync(ct);
            return entry is null ? null : Item(entry, withValue: true);
        }

        public Task SetAsync(StorageSlot slot, string key, object? value, TimeSpan? ttl, CancellationToken ct)
            => WriteAsync(slot, key, _ => value, ttl, keepExpiry: false, ct);

        public Task<object?> UpdateAsync(StorageSlot slot, string key, Func<object?, object?> update, TimeSpan? ttl, CancellationToken ct)
            => WriteAsync(slot, key, update, ttl, keepExpiry: true, ct);

        public async Task<bool> DeleteAsync(StorageSlot slot, string key, CancellationToken ct)
        {
            var entry = await In(slot).Where(e => e.Key == key).FirstOrDefaultAsync(ct);
            if (entry is null)
                return false;
            _db.StorageEntries.Remove(entry);
            await _db.SaveChangesAsync(ct);
            return !Expired(entry, Now);
        }

        public async Task<IReadOnlyList<StorageItem>> ListAsync(
            StorageSlot slot, string? prefix, string? after, int limit, bool values, CancellationToken ct)
        {
            var now = Now;
            var entries = In(slot).AsNoTracking().Where(e => e.ExpiresAt == null || e.ExpiresAt > now);
            if (!string.IsNullOrEmpty(prefix))
                entries = entries.Where(e => e.Key.StartsWith(prefix));
            if (!string.IsNullOrEmpty(after))
                entries = entries.Where(e => string.Compare(e.Key, after) > 0);

            var page = await entries.OrderBy(e => e.Key).Take(limit)
                .Select(e => new StorageEntry
                {
                    Key = e.Key,
                    Value = values ? e.Value : "null",
                    Size = e.Size,
                    UpdatedAt = e.UpdatedAt,
                    ExpiresAt = e.ExpiresAt,
                })
                .ToListAsync(ct);
            return page.Select(e => Item(e, values)).ToList();
        }

        private async Task<object?> WriteAsync(
            StorageSlot slot, string key, Func<object?, object?> update, TimeSpan? ttl, bool keepExpiry, CancellationToken ct)
        {
            if (key.Length > StorageEntryConfiguration.MaxKeyLength)
                throw new StorageLimitException($"Keys are at most {StorageEntryConfiguration.MaxKeyLength} characters.");

            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            if (_db.Database.IsNpgsql())
                await _db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({LockId(slot, key)})", ct);

            var now = Now;
            var entry = await In(slot).Where(e => e.Key == key).FirstOrDefaultAsync(ct);
            // The stored entry while it has not expired; null otherwise.
            var current = entry is not null && !Expired(entry, now) ? entry : null;
            var live = current is not null;

            var value = update(current is not null ? Parse(current.Value) : null);
            var json = Eval.ToJson(value);
            var size = Encoding.UTF8.GetByteCount(json);
            if (size > _options.MaxValueBytes)
                throw new StorageLimitException($"The value is {size} bytes as JSON; at most {_options.MaxValueBytes}.");

            await CheckQuotaAsync(slot.UserId, current?.Size ?? 0, size, isNew: !live, now, ct);

            if (entry is null)
            {
                entry = new StorageEntry { UserId = slot.UserId, WorkflowId = slot.WorkflowId, Key = key };
                _db.StorageEntries.Add(entry);
            }

            entry.Value = json;
            entry.Size = size;
            // An expired value is gone: its expiry goes with it.
            entry.ExpiresAt = ttl is { } t ? now + t : keepExpiry && live ? entry.ExpiresAt : null;
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return value;
        }

        private async Task CheckQuotaAsync(Guid userId, int oldSize, int newSize, bool isNew, DateTime now, CancellationToken ct)
        {
            var live = _db.StorageEntries.Where(e => e.UserId == userId && (e.ExpiresAt == null || e.ExpiresAt > now));
            if (isNew && await live.CountAsync(ct) >= _options.MaxEntriesPerUser)
                throw new StorageLimitException($"Storage holds at most {_options.MaxEntriesPerUser} keys; delete some first.");

            var bytes = await live.SumAsync(e => (long)e.Size, ct);
            if (bytes - oldSize + newSize > _options.MaxBytesPerUser)
                throw new StorageLimitException($"Storage holds at most {_options.MaxBytesPerUser} bytes; delete some first.");
        }

        // WorkflowId null (the shared scope) compares as IS NULL.
        private IQueryable<StorageEntry> In(StorageSlot slot)
            => _db.StorageEntries.Where(e => e.UserId == slot.UserId && e.WorkflowId == slot.WorkflowId);

        private static bool Expired(StorageEntry entry, DateTime now) => entry.ExpiresAt is { } at && at <= now;

        private static StorageItem Item(StorageEntry entry, bool withValue)
            => new(entry.Key, withValue ? Parse(entry.Value) : null, entry.Size, entry.UpdatedAt, entry.ExpiresAt);

        private static object? Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            return Eval.Plain(document.RootElement);
        }

        /// <summary>The advisory lock of one key: 64 bits of a hash of user, scope and key.</summary>
        public static long LockId(StorageSlot slot, string key)
        {
            var text = $"{slot.UserId:N}/{slot.WorkflowId?.ToString("N") ?? "-"}/{key}";
            return BinaryPrimitives.ReadInt64LittleEndian(XxHash64.Hash(Encoding.UTF8.GetBytes(text)));
        }
    }
}
