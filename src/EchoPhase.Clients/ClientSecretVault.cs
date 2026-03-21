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
        private const string IndexSuffix = "__index";

        public ClientSecretVault(
            IConnectionMultiplexer redis,
            AesGcm aesGcm,
            IOptions<DatabaseOptions> settings,
            JsonSerializerOptions? jsonOptions = null)
            : base(redis, aesGcm, settings, jsonOptions) { }

        // --------------------------
        // Key helpers
        // --------------------------

        private string UserKey(string userId, string keyName) =>
            $"client:{userId}:{keyName}";

        private string IndexKey(string userId) =>
            Prefixed($"client:{userId}{IndexSuffix}");

        // --------------------------
        // Index helpers
        // --------------------------

        private async Task<HashSet<string>> IndexGetAsync(string userId)
        {
            var raw = await Db.StringGetAsync(IndexKey(userId));
            if (!raw.HasValue) return new HashSet<string>();

            try
            {
                var decrypted = AesGcm.Decrypt((byte[])raw!);
                return JsonSerializer.Deserialize<HashSet<string>>(decrypted)
                    ?? new HashSet<string>();
            }
            catch
            {
                return new HashSet<string>();
            }
        }

        private async Task IndexSaveAsync(string userId, HashSet<string> index)
        {
            var encrypted = AesGcm.Encrypt(
                JsonSerializer.SerializeToUtf8Bytes(index));
            await Db.StringSetAsync(IndexKey(userId), encrypted);
        }

        private async Task IndexAddAsync(string userId, string keyName)
        {
            var index = await IndexGetAsync(userId);
            if (index.Add(keyName))
                await IndexSaveAsync(userId, index);
        }

        private async Task IndexRemoveAsync(string userId, string keyName)
        {
            var index = await IndexGetAsync(userId);
            if (index.Remove(keyName))
                await IndexSaveAsync(userId, index);
        }

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
            var result = await SetAsync(UserKey(userId, keyName), value, expiry, keepTtl, when, flags);

            if (result)
                await IndexAddAsync(userId, keyName);

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
            var result = Set(UserKey(userId, keyName), value, expiry, keepTtl, when, flags);

            if (result)
                IndexAddAsync(userId, keyName).GetAwaiter().GetResult();

            return result;
        }

        // --------------------------
        // GetOrSet
        // --------------------------

        public async Task<IServiceResult<T>> GetOrSetAsync<T>(
            string userId,
            string keyName,
            Func<Task<T>>? generator = null,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None)
        {
            var result = await GetOrSetAsync(UserKey(userId, keyName), generator, expiry, keepTtl, flags);
            if (result.Successful)
                await IndexAddAsync(userId, keyName);
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

        // --------------------------
        // Delete
        // --------------------------

        public async Task<bool> DeleteAsync(string userId, string keyName)
        {
            var result = await DeleteAsync(UserKey(userId, keyName));

            if (result)
                await IndexRemoveAsync(userId, keyName);

            return result;
        }

        public bool Delete(string userId, string keyName)
        {
            var result = Delete(UserKey(userId, keyName));

            if (result)
                IndexRemoveAsync(userId, keyName).GetAwaiter().GetResult();

            return result;
        }

        // --------------------------
        // Delete all user keys
        // --------------------------

        public async Task DeleteAllAsync(string userId)
        {
            var index = await IndexGetAsync(userId);

            var keys = index
                .Select(keyName => (RedisKey)Prefixed(UserKey(userId, keyName)))
                .Append((RedisKey)IndexKey(userId))
                .ToArray();

            await Db.KeyDeleteAsync(keys);
        }

        // --------------------------
        // Get all user key names
        // --------------------------

        public async Task<IEnumerable<string>> GetUserKeyNamesAsync(string userId) =>
            await IndexGetAsync(userId);

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
