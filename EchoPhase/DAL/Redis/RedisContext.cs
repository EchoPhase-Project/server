using EchoPhase.DAL.Redis.Models;
using EchoPhase.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace EchoPhase.DAL.Redis
{
    public class RedisContext : DbContext
    {
        public RedisContext(
            IDistributedCache cache,
            IOptions<RedisSettings> settings
        ) : base(cache, settings.Value)
        {
            RegisterSet<QrUserCache>($"tenant:{_settings.TenantId}:user:{{key}}:qr");
            RegisterSet<QrCache>($"tenant:{_settings.TenantId}:qr:{{key}}:user");
            RegisterSet<WebSocketCache>($"tenant:{_settings.TenantId}:webSocket:{{key}}:user");
        }
    }
}
