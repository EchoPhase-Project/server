using System.Security.Cryptography;
using EchoPhase.Interfaces;
using StackExchange.Redis;
using EchoPhase.Settings;
using Microsoft.Extensions.Options;

namespace EchoPhase.Services.Security
{
    /// <summary>
    /// Service for managing Redis keys with a fixed "key_" prefix.
    /// Provides both synchronous and asynchronous access to Redis key operations.
    /// </summary>
    public class KeysService : IKeysService
    {
        private readonly IDatabase _db;
        private readonly RedisSettings _settings;
        private const string KeyPrefix = "key_";

        /// <summary>
        /// Initializes the Redis keys service using the default Redis database (0).
        /// </summary>
        /// <param name="redis">Redis connection multiplexer</param>
        /// <param name="settings">Redis appsettings</param>
        public KeysService(
            IConnectionMultiplexer redis,
            IOptions<RedisSettings> settings
        )
        {
            _db = redis.GetDatabase(); // Default DB (0)
            _settings = settings.Value;
        }

        // --------------------------
        // Async Methods
        // --------------------------

        /// <summary>
        /// Asynchronously checks whether a key with prefix exists in Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix)</param>
        /// <returns>True if the key exists; otherwise, false</returns>
        public async Task<bool> ExistsAsync(string key)
        {
            return await _db.KeyExistsAsync(Prefixed(key));
        }

        /// <summary>
        /// Asynchronously retrieves the value of the prefixed key from Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix)</param>
        /// <returns>The string value stored in Redis</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the key does not exist</exception>
        public async Task<string> GetAsync(string key)
        {
            RedisValue value = await _db.StringGetAsync(Prefixed(key));
            if (value.IsNull)
                throw new KeyNotFoundException($"Key '{key}' not found.");
            return value!;
        }

        /// <summary>
        /// Asynchronously sets the value of the prefixed key in Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix)</param>
        /// <param name="value">The value to store</param>
        /// <returns>True if the value was set successfully</returns>
        public async Task<bool> SetAsync(string key, string value)
        {
            return await _db.StringSetAsync(Prefixed(key), value);
        }

        /// <summary>
        /// Asynchronously retrieves or sets a key. If the key does not exist, uses an optional async generator or default generator.
        /// </summary>
        /// <param name="key">The logical key (without prefix)</param>
        /// <param name="generator">Optional asynchronous generator for the value</param>
        /// <returns>The existing or newly generated value</returns>
        public async Task<string> GetOrSetAsync(string key, Func<Task<string>>? generator = null)
        {
            if (!await ExistsAsync(key))
            {
                var value = generator != null
                    ? await generator()
                    : GenerateRandomBase64(32);

                await SetAsync(key, value);
                return value;
            }

            return await GetAsync(key);
        }

        /// <summary>
        /// Asynchronously retrieves or sets a key. If the key does not exist, uses a synchronous generator function.
        /// </summary>
        /// <param name="key">The logical key (without prefix)</param>
        /// <param name="syncGenerator">Synchronous generator function for the value</param>
        /// <returns>The existing or newly generated value</returns>
        public Task<string> GetOrSetAsync(string key, Func<string> syncGenerator)
        {
            return GetOrSetAsync(key, () => Task.FromResult(syncGenerator()));
        }

        // --------------------------
        // Sync Methods
        // --------------------------

        /// <summary>
        /// Synchronously checks whether a key with prefix exists in Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix)</param>
        /// <returns>True if the key exists; otherwise, false</returns>
        public bool Exists(string key)
        {
            return _db.KeyExists(Prefixed(key));
        }

        /// <summary>
        /// Synchronously retrieves the value of the prefixed key from Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix)</param>
        /// <returns>The string value stored in Redis</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the key does not exist</exception>
        public string Get(string key)
        {
            RedisValue value = _db.StringGet(Prefixed(key));
            if (value.IsNull)
                throw new KeyNotFoundException($"Key '{key}' not found.");
            return value!;
        }

        /// <summary>
        /// Synchronously sets the value of the prefixed key in Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix)</param>
        /// <param name="value">The value to store</param>
        /// <returns>True if the value was set successfully</returns>
        public bool Set(string key, string value)
        {
            return _db.StringSet(Prefixed(key), value);
        }

        /// <summary>
        /// Synchronously retrieves or sets a key. If the key does not exist, uses a generator or default generator.
        /// </summary>
        /// <param name="key">The logical key (without prefix)</param>
        /// <param name="generator">Optional generator function for the value</param>
        /// <returns>The existing or newly generated value</returns>
        public string GetOrSet(string key, Func<string>? generator = null)
        {
            if (!Exists(key))
            {
                var value = generator != null
                    ? generator()
                    : GenerateRandomBase64();

                Set(key, value);
                return value;
            }

            return Get(key);
        }

        // --------------------------
        // Utilities
        // --------------------------

        /// <summary>
        /// Generates a cryptographically secure random Base64 string with the given byte length.
        /// </summary>
        /// <param name="byteLength">Length in bytes of the random data</param>
        /// <returns>A Base64-encoded string</returns>
        public static string GenerateRandomBase64(int byteLength = 32)
        {
            var randomBytes = new byte[byteLength];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        /// <summary>
        /// Returns the Redis key with the defined prefix.
        /// </summary>
        /// <param name="key">Logical key</param>
        /// <returns>Prefixed Redis key</returns>
        private string Prefixed(string key) => $"{_settings.InstanceName}{KeyPrefix}tenant:{_settings.TenantId}:{key}";
    }
}
