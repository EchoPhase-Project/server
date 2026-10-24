// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using StackExchange.Redis;

namespace EchoPhase.DAL.Redis.Maintenance
{
    /// <summary>
    /// One Redis key: every instance sees a switch at once, and nothing about
    /// it is configuration (no commit, no deploy). A set end is also the key's
    /// TTL, so a forgotten maintenance ends by itself.
    /// </summary>
    public sealed class RedisMaintenanceStore : IMaintenanceStore
    {
        private static readonly RedisKey Key = "maintenance:state";
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly Func<IConnectionMultiplexer> _redis;

        /// <param name="redis">Resolved per call: the connection opens on first use, as elsewhere.</param>
        public RedisMaintenanceStore(Func<IConnectionMultiplexer> redis)
        {
            _redis = redis;
        }

        public async Task<MaintenanceState?> GetAsync(CancellationToken ct = default)
        {
            var value = await _redis().GetDatabase().StringGetAsync(Key);
            return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<MaintenanceState>(value.ToString(), Json);
        }

        public async Task SetAsync(MaintenanceState state, TimeSpan? expiry, CancellationToken ct = default)
        {
            var db = _redis().GetDatabase();
            var transaction = db.CreateTransaction();
            var set = transaction.StringSetAsync(Key, JsonSerializer.Serialize(state, Json));
            var expire = expiry is { } ttl ? transaction.KeyExpireAsync(Key, ttl) : transaction.KeyPersistAsync(Key);
            if (!await transaction.ExecuteAsync())
                throw new RedisException("Maintenance state was not stored.");
            await Task.WhenAll(set, expire);
        }

        public async Task ClearAsync(CancellationToken ct = default)
            => await _redis().GetDatabase().KeyDeleteAsync(Key);
    }
}
