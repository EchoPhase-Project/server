// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using UUIDNext;

namespace EchoPhase.DAL.Redis.Database
{
    public class CacheSet<T>
    {
        private readonly string _template;
        private readonly IDistributedCache _cache;
        private readonly Guid _tenantId;
        private readonly ConcurrentDictionary<string, string> _keyCache = new();

        public CacheSet(
            IDistributedCache cache,
            string template,
            Guid tenantId)
        {
            _cache = cache;
            _template = template;
            _tenantId = tenantId;
        }

        private string GenerateCacheKey(string key) =>
            _keyCache.GetOrAdd(key, k =>
                Uuid.NewNameBased(_tenantId, _template.Replace("{key}", k)).ToString());

        public CacheEntry<T> this[string key]
        {
            get
            {
                string cacheKey = GenerateCacheKey(key);
                return new CacheEntry<T>(_cache, cacheKey, _tenantId, key);
            }
        }
    }
}
