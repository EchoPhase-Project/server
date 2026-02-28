using EchoPhase.Configuration.Database.Redis;
using EchoPhase.DAL.Redis.Models;
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
            RegisterSet<QrUserCache>("user:{{key}}:qr");
            RegisterSet<QrCache>("qr:{{key}}:user");
            RegisterSet<WebSocketCache>("webSocket:{{key}}:user");
        }
    }
}
