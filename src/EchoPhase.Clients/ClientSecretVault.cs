// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Configuration.Database;
using EchoPhase.Security.Cryptography;
using EchoPhase.Security.Cryptography.Vaults;
using EchoPhase.Types.Result;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EchoPhase.Clients
{
    public sealed class ClientSecretVault : SecretVaultBase, IClientSecretVault
    {
        protected override string KeyPrefix => "client_";

        private string IndexSetKey(string userId) =>
            $"{Settings.InstanceName}client_index:{Settings.TenantId}:{userId}";

        public ClientSecretVault(
            IConnectionMultiplexer redis,
            AesGcm aesGcm,
            IOptions<DatabaseOptions> settings,
            JsonSerializerOptions? jsonOptions = null)
            : base(redis, aesGcm, settings, jsonOptions)
        {
        }

        // ── Key helper ───────────────────────────────────────────────────────

        private string UserKey(string userId, string keyName) =>
            $"client:{userId}:{keyName}";

        // ── Exists ───────────────────────────────────────────────────────────

        public Task<bool> ExistsAsync(string userId, string keyName) =>
            ExistsAsync(UserKey(userId, keyName));

        public bool Exists(string userId, string keyName) =>
            Exists(UserKey(userId, keyName));

        // ── Get ──────────────────────────────────────────────────────────────

        public Task<IServiceResult<T>> GetAsync<T>(string userId, string keyName) =>
            GetAsync<T>(UserKey(userId, keyName));

        public IServiceResult<T> Get<T>(string userId, string keyName) =>
            Get<T>(UserKey(userId, keyName));

        // ── Set ──────────────────────────────────────────────────────────────
        //
        // The caller (ExternalTokenService) writes to the DB first, then calls
        // Set here.  If Redis is unavailable the DB row still exists, so on the
        // next read GetOrSetAsync will re-populate the cache from the DB.

        public async Task<bool> SetAsync<T>(
            string userId,
            string keyName,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None)
        {
            var stored = await SetAsync(UserKey(userId, keyName), value, expiry, keepTtl, when, flags);

            if (stored)
                // SADD is atomic and idempotent – safe to call on every write.
                await Db.SetAddAsync(IndexSetKey(userId), keyName);

            return stored;
        }

        public bool Set<T>(
            string userId,
            string keyName,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None)
        {
            var stored = Set(UserKey(userId, keyName), value, expiry, keepTtl, when, flags);

            if (stored)
                Db.SetAdd(IndexSetKey(userId), keyName);

            return stored;
        }

        // ── GetOrSet ─────────────────────────────────────────────────────────

        public async Task<IServiceResult<T>> GetOrSetAsync<T>(
            string userId,
            string keyName,
            Func<Task<T>>? generator = null,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None)
        {
            var result = await GetOrSetAsync(
                UserKey(userId, keyName), generator, expiry, keepTtl, flags);

            if (result.Successful)
                await Db.SetAddAsync(IndexSetKey(userId), keyName);

            return result;
        }

        public Task<IServiceResult<T>> GetOrSetAsync<T>(
            string userId,
            string keyName,
            Func<T> generator,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None) =>
            GetOrSetAsync(userId, keyName, () => Task.FromResult(generator()), expiry, keepTtl, flags);

        public IServiceResult<T> GetOrSet<T>(
            string userId,
            string keyName,
            Func<T>? generator = null,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None)
        {
            var result = GetOrSet(UserKey(userId, keyName), generator, expiry, keepTtl, flags);

            if (result.Successful)
                Db.SetAdd(IndexSetKey(userId), keyName);

            return result;
        }

        // ── Delete ───────────────────────────────────────────────────────────

        public async Task<bool> DeleteAsync(string userId, string keyName)
        {
            var deleted = await DeleteAsync(UserKey(userId, keyName));

            if (deleted)
                // SREM is atomic and idempotent.
                await Db.SetRemoveAsync(IndexSetKey(userId), keyName);

            return deleted;
        }

        public bool Delete(string userId, string keyName)
        {
            var deleted = Delete(UserKey(userId, keyName));

            if (deleted)
                Db.SetRemove(IndexSetKey(userId), keyName);

            return deleted;
        }

        /// <summary>
        /// Deletes all secrets for <paramref name="userId"/> plus the index Set
        /// in a single pipelined batch, avoiding multiple round-trips.
        /// </summary>
        public async Task DeleteAllAsync(string userId)
        {
            var members = await Db.SetMembersAsync(IndexSetKey(userId));

            // Build list: one entry per secret key + the index Set itself.
            var redisKeys = members
                .Select(m => (RedisKey)Prefixed(UserKey(userId, m!)))
                .Append((RedisKey)IndexSetKey(userId))
                .ToArray();

            if (redisKeys.Length > 0)
                await Db.KeyDeleteAsync(redisKeys);
        }

        // ── Index ────────────────────────────────────────────────────────────

        public async Task<IEnumerable<string>> GetUserKeyNamesAsync(string userId)
        {
            var members = await Db.SetMembersAsync(IndexSetKey(userId));
            return members.Select(m => m.ToString()).ToList();
        }

        public IEnumerable<string> GetUserKeyNames(string userId)
        {
            var members = Db.SetMembers(IndexSetKey(userId));
            return members.Select(m => m.ToString()).ToList();
        }

        public async Task<IDictionary<string, IServiceResult<T>>> GetAllAsync<T>(string userId)
        {
            var keyNames = await GetUserKeyNamesAsync(userId);
            var result = new Dictionary<string, IServiceResult<T>>();

            foreach (var keyName in keyNames)
                result[keyName] = await GetAsync<T>(userId, keyName);

            return result;
        }

        public IDictionary<string, IServiceResult<T>> GetAll<T>(string userId)
        {
            var keyNames = GetUserKeyNames(userId);
            var result = new Dictionary<string, IServiceResult<T>>();

            foreach (var keyName in keyNames)
                result[keyName] = Get<T>(userId, keyName);

            return result;
        }

        // ── Private helper ───────────────────────────────────────────────────

        /// <summary>
        /// Returns the full Redis key used by <see cref="SecretVaultBase"/> for
        /// a logical key name.  Needed for bulk delete.
        /// </summary>
        private string Prefixed(string logicalKey)
        {
            // SecretVaultBase.Prefixed is private; replicate its logic here so
            // DeleteAllAsync can compute the same cache key without reflection.
            // The formula is: InstanceName + UUID-v5(TenantId, KeyPrefix+TenantId+logicalKey)
            var raw = $"{KeyPrefix}tenant:{Settings.TenantId}:{logicalKey}";
            var uuid = UUIDNext.Uuid.NewNameBased(Settings.TenantId, raw);
            return $"{Settings.InstanceName}{uuid}";
        }
    }
}
