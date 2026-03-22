// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using EchoPhase.Configuration.Database.Redis;
using UUIDNext;

namespace EchoPhase.Security.Cryptography.Vaults.Strategies
{
    /// <summary>
    /// Default key strategy: generates a UUID-v5 key scoped by tenant ID.
    /// The key cache is held inside this class so each vault instance can share
    /// or isolate its own cache.
    /// </summary>
    public sealed class TenantUuidKeyStrategy : IKeyStrategy
    {
        private readonly string _keyPrefix;
        private readonly string _instanceName;
        private readonly Guid _tenantId;
        private readonly ConcurrentDictionary<string, string> _keyCache = new();

        /// <summary>
        /// Initialises the strategy from raw components.
        /// </summary>
        /// <param name="keyPrefix">
        /// Short prefix that distinguishes vault types, e.g. <c>"key_"</c> or <c>"secret_"</c>.
        /// </param>
        /// <param name="instanceName">Redis instance name (prepended to every key).</param>
        /// <param name="tenantId">Tenant identifier used to namespace the UUID derivation.</param>
        public TenantUuidKeyStrategy(string keyPrefix, string instanceName, Guid tenantId)
        {
            _keyPrefix = keyPrefix;
            _instanceName = instanceName;
            _tenantId = tenantId;
        }

        /// <summary>
        /// Initialises the strategy from <see cref="IRedisOptions"/> and a prefix.
        /// </summary>
        public TenantUuidKeyStrategy(string keyPrefix, IRedisOptions redisOptions)
            : this(keyPrefix, redisOptions.InstanceName, redisOptions.TenantId)
        {
        }

        /// <inheritdoc />
        public string Build(string key) =>
            _keyCache.GetOrAdd(
                $"{_keyPrefix}:{_tenantId}:{key}",
                raw =>
                {
                    var uuid = Uuid.NewNameBased(_tenantId, raw);
                    return $"{_instanceName}{uuid}";
                });
    }
}
