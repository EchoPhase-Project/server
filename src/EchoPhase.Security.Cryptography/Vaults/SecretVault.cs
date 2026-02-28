using EchoPhase.Configuration.Database.Redis;
using EchoPhase.Types.Result;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using System.Text;

namespace EchoPhase.Security.Cryptography.Vaults
{
    public sealed class SecretVault : ISecretVault
    {
        private readonly IDatabase _db;
        private readonly AesGcm _aesGcm;
        private readonly RedisSettings _settings;
        private readonly JsonSerializerOptions _jsonOptions;
        private const string KeyPrefix = "secret_";

        public SecretVault(
            IConnectionMultiplexer redis,
            AesGcm aesGcm,
            IOptions<RedisSettings> settings,
            JsonSerializerOptions? jsonOptions = null)
        {
            _db = redis.GetDatabase();
            _aesGcm = aesGcm;
            _settings = settings.Value;
            _jsonOptions = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        // --------------------------
        // Exists
        // --------------------------

        public Task<bool> ExistsAsync(string key) =>
            _db.KeyExistsAsync(Prefixed(key));

        public bool Exists(string key) =>
            _db.KeyExists(Prefixed(key));

        // --------------------------
        // Get
        // --------------------------

        public async Task<IServiceResult<T>> GetAsync<T>(string key)
        {
            if (!await ExistsAsync(key))
                return NotFound<T>(key);

            var raw = await _db.StringGetAsync(Prefixed(key));
            if (!raw.HasValue)
                return NotFound<T>(key);

            return Deserialize<T>(key, _aesGcm.Decrypt((byte[])raw!));
        }

        public IServiceResult<T> Get<T>(string key)
        {
            if (!Exists(key))
                return NotFound<T>(key);

            var raw = _db.StringGet(Prefixed(key));
            if (!raw.HasValue)
                return NotFound<T>(key);

            return Deserialize<T>(key, _aesGcm.Decrypt((byte[])raw!));
        }

        // --------------------------
        // Set
        // --------------------------

        public async Task<bool> SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None)
        {
            var encrypted = _aesGcm.Encrypt(Serialize(value));
            return await _db.StringSetAsync(Prefixed(key), encrypted, expiry, keepTtl, when, flags);
        }

        public bool Set<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None)
        {
            var encrypted = _aesGcm.Encrypt(Serialize(value));
            return _db.StringSet(Prefixed(key), encrypted, expiry, keepTtl, when, flags);
        }

        // --------------------------
        // GetOrSet
        // --------------------------

        public async Task<IServiceResult<T>> GetOrSetAsync<T>(string key, Func<Task<T>>? generator = null)
        {
            if (await ExistsAsync(key))
                return await GetAsync<T>(key);

            generator ??= () => Task.FromResult(default(T)!);
            var value = await generator();

            if (await SetAsync(key, value, when: When.NotExists))
                return ServiceResult<T>.Success(value);

            return ServiceResult<T>.Failure(err =>
                err.Set("InvalidOperation", $"Failed to save '{key}' to storage."));
        }

        public Task<IServiceResult<T>> GetOrSetAsync<T>(string key, Func<T> generator) =>
            GetOrSetAsync<T>(key, () => Task.FromResult(generator()));

        public IServiceResult<T> GetOrSet<T>(string key, Func<T>? generator = null)
        {
            if (Exists(key))
                return Get<T>(key);

            generator ??= () => default!;
            var value = generator();

            if (Set(key, value, when: When.NotExists))
                return ServiceResult<T>.Success(value);

            return ServiceResult<T>.Failure(err =>
                err.Set("InvalidOperation", $"Failed to save '{key}' to storage."));
        }

        // --------------------------
        // Delete
        // --------------------------

        public Task<bool> DeleteAsync(string key) =>
            _db.KeyDeleteAsync(Prefixed(key));

        public bool Delete(string key) =>
            _db.KeyDelete(Prefixed(key));

        // --------------------------
        // Serialization
        // --------------------------

        private byte[] Serialize<T>(T value)
        {
            return value switch
            {
                byte[] bytes => bytes,
                string str   => Encoding.UTF8.GetBytes(str),
                _            => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, _jsonOptions))
            };
        }

        private IServiceResult<T> Deserialize<T>(string key, byte[] raw)
        {
            try
            {
                if (typeof(T) == typeof(byte[]))
                    return ServiceResult<T>.Success((T)(object)raw);

                var str = Encoding.UTF8.GetString(raw);

                if (typeof(T) == typeof(string))
                    return ServiceResult<T>.Success((T)(object)str);

                var deserialized = JsonSerializer.Deserialize<T>(str, _jsonOptions);
                if (deserialized is null)
                    return NotFound<T>(key);

                return ServiceResult<T>.Success(deserialized);
            }
            catch (Exception ex)
            {
                return ServiceResult<T>.Failure(err =>
                    err.Set("DeserializationError", $"Failed to deserialize '{key}': {ex.Message}"));
            }
        }

        private static IServiceResult<T> NotFound<T>(string key) =>
            ServiceResult<T>.Failure(err =>
                err.Set("KeyNotFound", $"Failed to retrieve '{key}' from storage."));

        private string Prefixed(string key) =>
            $"{_settings.InstanceName}{KeyPrefix}tenant:{_settings.TenantId}:{key}";
    }
}
