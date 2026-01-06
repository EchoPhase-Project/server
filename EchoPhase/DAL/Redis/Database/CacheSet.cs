using Microsoft.Extensions.Caching.Distributed;

namespace EchoPhase.DAL.Redis
{
    public class CacheSet<T>
    {
        private readonly string _template;
        private readonly IDistributedCache _cache;

        public CacheSet(
                IDistributedCache cache,
                string template)
        {
            _cache = cache;
            _template = template;
        }

        private string FormatKey(string key) =>
            _template.Replace("{key}", key);

        public CacheEntry<T> this[string key] =>
            new CacheEntry<T>(_cache, FormatKey(key));
    }
}
