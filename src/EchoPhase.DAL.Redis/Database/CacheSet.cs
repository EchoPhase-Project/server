using Microsoft.Extensions.Caching.Distributed;
using UUIDNext;
using System.Collections.Concurrent;

namespace EchoPhase.DAL.Redis
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
