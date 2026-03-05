using EchoPhase.Configuration.Database;
using EchoPhase.DAL.Redis.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace EchoPhase.DAL.Redis
{
    public class RedisContext : DbContext
    {
        public RedisContext(
            IDistributedCache cache,
            IOptions<DatabaseOptions> settings
        ) : base(cache, settings.Value.Redis)
        {
            RegisterSet<QrUserCache>("user:{{key}}:qr");
            RegisterSet<QrCache>("qr:{{key}}:user");
            RegisterSet<WebSocketCache>("webSocket:{{key}}:user");
            RegisterSet<SecurityStamp>("user:{{key}}:securityStamp");
        }
    }
}
