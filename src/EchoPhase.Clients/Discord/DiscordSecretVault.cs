using System.Text.Json;
using Microsoft.Extensions.Options;
using EchoPhase.Configuration.Database.Redis;
using EchoPhase.Security.Cryptography;
using EchoPhase.Security.Cryptography.Vaults;
using EchoPhase.Types.Result;
using StackExchange.Redis;

// --------------------------
// Discord
// --------------------------

namespace EchoPhase.Clients.Discord
{
    public sealed class DiscordSecretVault : SecretVaultBase, IDiscordSecretVault
    {
        protected override string KeyPrefix => "discord_";
        private const string IndexSuffix = "__index";

        public DiscordSecretVault(
            IConnectionMultiplexer redis,
            AesGcm aesGcm,
            IOptions<RedisOptions> settings,
            JsonSerializerOptions? jsonOptions = null)
            : base(redis, aesGcm, settings, jsonOptions) { }

        // --------------------------
        // Key helpers
        // --------------------------

        private string UserKey(string userId, string keyName) =>
            $"discord:{userId}:{keyName}";

        private string IndexKey(string userId) =>
            Prefixed($"discord:{userId}{IndexSuffix}");

        // --------------------------
        // Exists
        // --------------------------

        public Task<bool> ExistsAsync(string userId, string keyName) =>
            ExistsAsync(UserKey(userId, keyName));

        public bool Exists(string userId, string keyName) =>
            Exists(UserKey(userId, keyName));

        // --------------------------
        // Get
        // --------------------------

        public Task<IServiceResult<T>> GetAsync<T>(string userId, string keyName) =>
            GetAsync<T>(UserKey(userId, keyName));

        public IServiceResult<T> Get<T>(string userId, string keyName) =>
            Get<T>(UserKey(userId, keyName));

        // --------------------------
        // Set
        // --------------------------

        public async Task<bool> SetAsync<T>(
            string userId,
            string keyName,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None)
        {
            var key = UserKey(userId, keyName);
            var result = await SetAsync(key, value, expiry, keepTtl, when, flags);

            if (result)
                await Db.SetAddAsync(IndexKey(userId), keyName);

            return result;
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
            var key = UserKey(userId, keyName);
            var result = Set(key, value, expiry, keepTtl, when, flags);

            if (result)
                Db.SetAdd(IndexKey(userId), keyName);

            return result;
        }

        // --------------------------
        // GetOrSet
        // --------------------------

        public async Task<IServiceResult<T>> GetOrSetAsync<T>(
            string userId,
            string keyName,
            Func<Task<T>>? generator = null)
        {
            var key = UserKey(userId, keyName);
            var result = await GetOrSetAsync(key, generator);

            if (result.Successful)
                await Db.SetAddAsync(IndexKey(userId), keyName);

            return result;
        }

        public Task<IServiceResult<T>> GetOrSetAsync<T>(
            string userId,
            string keyName,
            Func<T> generator) =>
            GetOrSetAsync(userId, keyName, () => Task.FromResult(generator()));

        // --------------------------
        // Delete
        // --------------------------

        public async Task<bool> DeleteAsync(string userId, string keyName)
        {
            var result = await DeleteAsync(UserKey(userId, keyName));

            if (result)
                await Db.SetRemoveAsync(IndexKey(userId), keyName);

            return result;
        }

        public bool Delete(string userId, string keyName)
        {
            var result = Delete(UserKey(userId, keyName));

            if (result)
                Db.SetRemove(IndexKey(userId), keyName);

            return result;
        }

        // --------------------------
        // Delete all user keys
        // --------------------------

        public async Task DeleteAllAsync(string userId)
        {
            var members = await Db.SetMembersAsync(IndexKey(userId));

            var keys = members
                .Select(m => (RedisKey)Prefixed(UserKey(userId, m.ToString())))
                .Append((RedisKey)IndexKey(userId))
                .ToArray();

            await Db.KeyDeleteAsync(keys);
        }

        // --------------------------
        // Get all user keys
        // --------------------------

        public async Task<IEnumerable<string>> GetUserKeyNamesAsync(string userId)
        {
            var members = await Db.SetMembersAsync(IndexKey(userId));
            return members.Select(m => m.ToString());
        }

        // --------------------------
        // Get all user secrets
        // --------------------------

        public async Task<IDictionary<string, IServiceResult<T>>> GetAllAsync<T>(string userId)
        {
            var keyNames = await GetUserKeyNamesAsync(userId);
            var result = new Dictionary<string, IServiceResult<T>>();

            foreach (var keyName in keyNames)
                result[keyName] = await GetAsync<T>(userId, keyName);

            return result;
        }
    }
}
