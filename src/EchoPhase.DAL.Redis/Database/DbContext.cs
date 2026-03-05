using EchoPhase.Configuration.Database.Redis;
using EchoPhase.DAL.Redis.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Concurrent;

namespace EchoPhase.DAL.Redis
{
    public class DbContext : ICacheContext
    {
        private readonly ConcurrentDictionary<Type, object> _sets = new();
        private readonly IDistributedCache _cache;
        protected readonly IRedisOptions _settings;

        public DbContext(
            IDistributedCache cache,
            IRedisOptions settings
        )
        {
            _cache = cache;
            _settings = settings;
        }


        protected void RegisterSet<T>(string template)
        {
            var set = new CacheSet<T>(_cache, template, _settings.TenantId);
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
