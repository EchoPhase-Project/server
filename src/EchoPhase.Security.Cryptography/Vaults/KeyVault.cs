using System.Security.Cryptography;
using EchoPhase.Configuration.Database;
using EchoPhase.Configuration.Database.Redis;
using EchoPhase.Types.Result;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EchoPhase.Security.Cryptography.Vaults
{
    /// <summary>
    /// Service for managing Redis keys with a fixed "key_" prefix.
    /// Provides both synchronous and asynchronous access to Redis key operations.
    /// </summary>
    public sealed class KeyVault : IKeyVault
    {
        private readonly IDatabase _db;
        private readonly RedisOptions _settings;
        private const string KeyPrefix = "key_";

        /// <summary>
        /// Initializes the Redis keys service using the default Redis database (0).
        /// </summary>
        /// <param name="redis">Redis connection multiplexer</param>
        /// <param name="settings">Redis appsettings</param>
        public KeyVault(
            IConnectionMultiplexer redis,
            IOptions<DatabaseOptions> settings
        )
        {
            _db = redis.GetDatabase(); // Default DB (0)
            _settings = settings.Value.Redis;
        }

        // --------------------------
        // Async Methods
        // --------------------------

        /// <summary>
        /// Asynchronously checks whether a key with prefix exists in Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains <c>true</c> if the key exists; otherwise, <c>false</c>.
        /// </returns>
        public Task<bool> ExistsAsync(string key)
        {
            return _db.KeyExistsAsync(Prefixed(key));
        }

        /// <summary>
        /// Asynchronously retrieves the value of the prefixed key from Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="IServiceResult{T}"/> containing:
        /// - the byte array if the key exists;
        /// - an error result if the key does not exist.
        /// </returns>
        public async Task<IServiceResult<byte[]>> GetAsync(string key)
        {
            if (!(await ExistsAsync(key)))
                return ServiceResult<byte[]>.Failure(err =>
                    err.Set("KeyNotFound", $"Failed to retrieve '{key}' key from storage."));

            return ServiceResult<byte[]>.Success(await _db.StringGetAsync(Prefixed(key)));
        }

        /// <summary>
        /// Asynchronously sets the value of the prefixed key in Redis with additional options.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <param name="value">The value to store.</param>
        /// <param name="expiry">Optional expiration time for the key. If null, the key will persist unless overwritten or deleted.</param>
        /// <param name="keepTtl">If <c>true</c>, preserves the existing TTL if the key already exists. Ignored if <paramref name="expiry"/> is set.</param>
        /// <param name="when">The condition under which the value should be set (e.g. only if key does not exist).</param>
        /// <param name="flags">Optional command flags (e.g. <see cref="CommandFlags.FireAndForget"/>).</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains <c>true</c> if the value was set; otherwise, <c>false</c>.
        /// </returns>
        public Task<bool> SetAsync(
            string key,
            byte[] value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None)
        {
            var redisKey = Prefixed(key);
            return _db.StringSetAsync(redisKey, value, expiry, keepTtl, when, flags);
        }

        /// <summary>
        /// Asynchronously retrieves the value for the given key. If the key does not exist, generates the value using the provided asynchronous generator, stores it, and returns it.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <param name="generator">An optional asynchronous function that generates the value if the key is not found. If <c>null</c>, a default random generator is used.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="IServiceResult{T}"/> with:
        /// - the existing or newly generated value if successful;
        /// - an error result if the value could not be stored.
        /// </returns>
        public async Task<IServiceResult<byte[]>> GetOrSetAsync(string key, Func<Task<byte[]>>? generator = null)
        {
            if (await ExistsAsync(key))
                return await GetAsync(key);

            generator ??= () => Task.FromResult(GenerateRandom());
            var value = await generator();
            if (await SetAsync(key, value, when: When.NotExists))
                return ServiceResult<byte[]>.Success(value);

            return ServiceResult<byte[]>.Failure(err =>
                err.Set("InvalidOperation", $"Failed to save '{key}' key to storage."));
        }

        /// <summary>
        /// Asynchronously retrieves the value for the given key. If the key does not exist, generates the value using the provided synchronous generator, stores it, and returns it.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <param name="syncGenerator">A synchronous function that generates the value if the key is not found.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="IServiceResult{T}"/> with:
        /// - the existing or newly generated value if successful;
        /// - an error result if the value could not be stored.
        /// </returns>
        public Task<IServiceResult<byte[]>> GetOrSetAsync(string key, Func<byte[]> syncGenerator)
        {
            return GetOrSetAsync(key, () => Task.FromResult(syncGenerator()));
        }

        // --------------------------
        // Sync Methods
        // --------------------------

        /// <summary>
        /// Synchronously checks whether a key with prefix exists in Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <returns><c>true</c> if the key exists; otherwise, <c>false</c>.</returns>
        public bool Exists(string key)
        {
            return _db.KeyExists(Prefixed(key));
        }

        /// <summary>
        /// Synchronously retrieves the value of the prefixed key from Redis.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <returns>
        /// A <see cref="IServiceResult{T}"/> containing:
        /// - the byte array if the key exists;
        /// - an error result if the key does not exist.
        /// </returns>
        public IServiceResult<byte[]> Get(string key)
        {
            if (!Exists(key))
                return ServiceResult<byte[]>.Failure(err =>
                    err.Set("KeyNotFound", $"Failed to retrieve '{key}' key from storage."));

            return ServiceResult<byte[]>.Success(_db.StringGet(Prefixed(key)));
        }

        /// <summary>
        /// Synchronously sets the value of the prefixed key in Redis with additional options.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <param name="value">The value to store.</param>
        /// <param name="expiry">Optional expiration time for the key. If <c>null</c>, the key will persist unless overwritten or deleted.</param>
        /// <param name="keepTtl">If <c>true</c>, preserves the existing TTL if the key already exists. Ignored if <paramref name="expiry"/> is set.</param>
        /// <param name="when">The condition under which the value should be set (e.g. only if key does not exist).</param>
        /// <param name="flags">Optional command flags (e.g. <see cref="CommandFlags.FireAndForget"/>).</param>
        /// <returns><c>true</c> if the value was set successfully; otherwise, <c>false</c>.</returns>
        public bool Set(
            string key,
            byte[] value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None)
        {
            return _db.StringSet(Prefixed(key), value, expiry, keepTtl, when, flags);
        }

        /// <summary>
        /// Synchronously retrieves the value for the given key. If the key does not exist, generates the value using the provided generator, stores it, and returns it.
        /// </summary>
        /// <param name="key">The logical key (without prefix).</param>
        /// <param name="generator">An optional synchronous function that generates the value if the key is not found. If <c>null</c>, a default generator is used.</param>
        /// <returns>
        /// A <see cref="IServiceResult{T}"/> containing:
        /// - the existing or newly generated value if successful;
        /// - an error result if the value could not be stored.
        /// </returns>
        public IServiceResult<byte[]> GetOrSet(string key, Func<byte[]>? generator = null)
        {
            if (Exists(key))
                return Get(key);

            generator ??= GenerateRandom;
            var value = generator();
            if (Set(key, value, when: When.NotExists))
                return ServiceResult<byte[]>.Success(value);

            return ServiceResult<byte[]>.Failure(err =>
                err.Set("InvalidOperation", $"Failed to save '{key}' key to storage."));
        }

        // --------------------------
        // Utilities
        // --------------------------

        /// <summary>
        /// Generates a cryptographically secure random byte array of default size (32 bytes).
        /// </summary>
        /// <returns>A byte array containing 32 random bytes.</returns>
        public static byte[] GenerateRandom() =>
            GenerateRandom(32);

        /// <summary>
        /// Generates a cryptographically secure random byte array of the specified size.
        /// </summary>
        /// <param name="size">The number of random bytes to generate.</param>
        /// <returns>A byte array containing <paramref name="size"/> random bytes.</returns>
        public static byte[] GenerateRandom(int size)
        {
            var data = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(data);
            return data;
        }

        /// <summary>
        /// Returns the Redis key with the defined prefix.
        /// </summary>
        /// <param name="key">Logical key</param>
        /// <returns>Prefixed Redis key</returns>
        private string Prefixed(string key) => $"{_settings.InstanceName}{KeyPrefix}tenant:{_settings.TenantId}:{key}";
    }
}
