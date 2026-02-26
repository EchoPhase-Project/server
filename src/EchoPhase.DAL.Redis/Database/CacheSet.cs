using Microsoft.Extensions.Caching.Distributed;
using UUIDNext;

namespace EchoPhase.DAL.Redis
{
    public class CacheSet<T>
    {
        private readonly string _template;
        private readonly IDistributedCache _cache;
        private readonly Guid _tenantId;

        public CacheSet(
            IDistributedCache cache,
            string template,
            Guid tenantId)
        {
            _cache = cache;
            _template = template;
            _tenantId = tenantId;
        }

        private string GenerateCacheKey(string key)
        {
            string raw = _template.Replace("{key}", key);
            Guid uuidKey = Uuid.NewNameBased(_tenantId, raw);
            return uuidKey.ToString();
        }

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
