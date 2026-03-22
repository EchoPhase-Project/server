// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using System.Text.Json;
using EchoPhase.Configuration.Database;
using EchoPhase.Configuration.Database.Redis;
using EchoPhase.Security.Cryptography.Vaults.Strategies;
using EchoPhase.Types.Result;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

// --------------------------
// Base
// --------------------------

namespace EchoPhase.Security.Cryptography.Vaults
{
    public abstract class SecretVaultBase
    {
        protected readonly IDatabase Db;
        protected readonly IConnectionMultiplexer Redis;
        protected readonly AesGcm AesGcm;
        protected readonly JsonSerializerOptions JsonOptions;

        /// <summary>
        /// Redis options — available when the <see cref="DatabaseOptions"/> convenience
        /// constructor is used.  Exposed so subclasses (e.g. <c>ClientSecretVault</c>)
        /// can build additional keys such as index sets without bypassing the strategy.
        /// </summary>
        protected readonly RedisOptions Settings;

        /// <summary>Strategy that translates logical key names to Redis storage keys.</summary>
        private readonly IKeyStrategy _keyStrategy;

        // --------------------------
        // Constructors
        // --------------------------

        /// <summary>
        /// Primary constructor — accepts an explicit <see cref="IKeyStrategy"/>.
        /// Use this for full control over key generation (custom tenant scope, testing, etc.).
        /// </summary>
        protected SecretVaultBase(
            IConnectionMultiplexer redis,
            AesGcm aesGcm,
            IKeyStrategy keyStrategy,
            RedisOptions redisOptions,
            JsonSerializerOptions? jsonOptions = null)
        {
            Redis = redis;
            Db = redis.GetDatabase();
            AesGcm = aesGcm;
            _keyStrategy = keyStrategy;
            Settings = redisOptions;
            JsonOptions = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// Convenience constructor — builds a <see cref="TenantUuidKeyStrategy"/> automatically
        /// from <see cref="DatabaseOptions"/> and the supplied <paramref name="keyPrefix"/>.
        /// Retains full backward compatibility for <see cref="SecretVault"/> and
        /// <see cref="ClientSecretVault"/> (neither needs to change its constructor signature).
        /// The prefix is passed explicitly because virtual/abstract members cannot be
        /// called from within a constructor chain.
        /// </summary>
        protected SecretVaultBase(
            IConnectionMultiplexer redis,
            AesGcm aesGcm,
            string keyPrefix,
            IOptions<DatabaseOptions> settings,
            JsonSerializerOptions? jsonOptions = null)
            : this(
                redis,
                aesGcm,
                new TenantUuidKeyStrategy(keyPrefix, settings.Value.Redis),
                settings.Value.Redis,
                jsonOptions)
        {
        }

        // --------------------------
        // Exists
        // --------------------------

        public Task<bool> ExistsAsync(string key) =>
            Db.KeyExistsAsync(Prefixed(key));

        public bool Exists(string key) =>
            Db.KeyExists(Prefixed(key));

        // --------------------------
        // Get
        // --------------------------

        public async Task<IServiceResult<T>> GetAsync<T>(string key)
        {
            if (!await ExistsAsync(key))
                return NotFound<T>(key);

            var raw = await Db.StringGetAsync(Prefixed(key));
            if (!raw.HasValue)
                return NotFound<T>(key);

            return Deserialize<T>(key, AesGcm.Decrypt((byte[])raw!));
        }

        public IServiceResult<T> Get<T>(string key)
        {
            if (!Exists(key))
                return NotFound<T>(key);

            var raw = Db.StringGet(Prefixed(key));
            if (!raw.HasValue)
                return NotFound<T>(key);

            return Deserialize<T>(key, AesGcm.Decrypt((byte[])raw!));
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
            var encrypted = AesGcm.Encrypt(Serialize(value));
            return await Db.StringSetAsync(Prefixed(key), encrypted, expiry, keepTtl, when, flags);
        }

        public bool Set<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            When when = When.Always,
            CommandFlags flags = CommandFlags.None)
        {
            var encrypted = AesGcm.Encrypt(Serialize(value));
            return Db.StringSet(Prefixed(key), encrypted, expiry, keepTtl, when, flags);
        }

        // --------------------------
        // GetOrSet
        // --------------------------

        public async Task<IServiceResult<T>> GetOrSetAsync<T>(
            string key,
            Func<Task<T>>? generator = null,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None)
        {
            if (await ExistsAsync(key))
                return await GetAsync<T>(key);
            generator ??= () => Task.FromResult(default(T)!);
            var value = await generator();
            if (await SetAsync(key, value, expiry, keepTtl, When.NotExists, flags))
                return ServiceResult<T>.Success(value);
            return ServiceResult<T>.Failure(err =>
                err.Set("InvalidOperation", $"Failed to save '{key}' to storage."));
        }

        public Task<IServiceResult<T>> GetOrSetAsync<T>(
            string key,
            Func<T> generator,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None) =>
            GetOrSetAsync<T>(key, () => Task.FromResult(generator()), expiry, keepTtl, flags);

        public IServiceResult<T> GetOrSet<T>(
            string key,
            Func<T>? generator = null,
            TimeSpan? expiry = null,
            bool keepTtl = false,
            CommandFlags flags = CommandFlags.None)
        {
            if (Exists(key))
                return Get<T>(key);
            generator ??= () => default!;
            var value = generator();
            if (Set(key, value, expiry, keepTtl, When.NotExists, flags))
                return ServiceResult<T>.Success(value);
            return ServiceResult<T>.Failure(err =>
                err.Set("InvalidOperation", $"Failed to save '{key}' to storage."));
        }

        // --------------------------
        // Delete
        // --------------------------

        public Task<bool> DeleteAsync(string key) =>
            Db.KeyDeleteAsync(Prefixed(key));

        public bool Delete(string key) =>
            Db.KeyDelete(Prefixed(key));

        // --------------------------
        // Serialization
        // --------------------------

        protected byte[] Serialize<T>(T value) => value switch
        {
            byte[] bytes => bytes,
            string str => Encoding.UTF8.GetBytes(str),
            _ => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))
        };

        protected IServiceResult<T> Deserialize<T>(string key, byte[] raw)
        {
            try
            {
                if (typeof(T) == typeof(byte[]))
                    return ServiceResult<T>.Success((T)(object)raw);

                var str = Encoding.UTF8.GetString(raw);

                if (typeof(T) == typeof(string))
                    return ServiceResult<T>.Success((T)(object)str);

                var deserialized = JsonSerializer.Deserialize<T>(str, JsonOptions);
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

        protected static IServiceResult<T> NotFound<T>(string key) =>
            ServiceResult<T>.Failure(err =>
                err.Set("KeyNotFound", $"Failed to retrieve '{key}' from storage."));

        // --------------------------
        // Key generation — delegated to IKeyStrategy
        // --------------------------

        /// <summary>
        /// Translates a logical key into the final Redis key via the injected <see cref="IKeyStrategy"/>.
        /// </summary>
        protected string Prefixed(string key) => _keyStrategy.Build(key);
    }
}
