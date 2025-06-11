using EchoPhase.DAL.Redis.Models;
using EchoPhase.Interfaces;
using EchoPhase.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace EchoPhase.DAL.Redis
{
    public class RedisContext : ICacheContext
    {
        private readonly Dictionary<Type, object> _sets = new();

        private readonly IDistributedCache _cache;
        private readonly RedisSettings _settings;

        public RedisContext(
            IDistributedCache cache,
            IOptions<RedisSettings> settings
        )
        {
            _cache = cache;
            _settings = settings.Value;

            RegisterSet<QrUserCache>($"tenant:{_settings.TenantId}:user:{{key}}:qr");
            RegisterSet<QrCache>($"tenant:{_settings.TenantId}:qr:{{key}}:user");
            RegisterSet<WebSocketCache>($"tenant:{_settings.TenantId}:webSocket:{{key}}:user");
        }

        private void RegisterSet<T>(string template)
        {
            var set = new RedisSet<T>(_cache, template);
            _sets[typeof(T)] = set;
        }

        public RedisEntityEntry<T> Entry<T>(string key)
        {
            if (_sets.TryGetValue(typeof(T), out var obj) && obj is RedisSet<T> set)
            {
                return set[key];
            }

            throw new InvalidOperationException($"Redis set for type '{typeof(T).Name}' not registered.");
        }
    }
}
