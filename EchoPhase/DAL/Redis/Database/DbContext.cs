using EchoPhase.DAL.Redis.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace EchoPhase.DAL.Redis
{
    public class DbContext : ICacheContext
    {
        private readonly Dictionary<Type, object> _sets = new();
        private readonly IDistributedCache _cache;
        protected readonly IRedisSettings _settings;

        public DbContext(
            IDistributedCache cache,
            IRedisSettings settings
        )
        {
            _cache = cache;
            _settings = settings;
        }

        protected void RegisterSet<T>(string template)
        {
            var set = new CacheSet<T>(_cache, template);
            _sets[typeof(T)] = set;
        }

        public CacheEntry<T> Entry<T>(string key)
        {
            if (_sets.TryGetValue(typeof(T), out var obj) && obj is CacheSet<T> set)
            {
                return set[key];
            }

            throw new InvalidOperationException($"Redis set for type '{typeof(T).Name}' not registered.");
        }
    }
}
