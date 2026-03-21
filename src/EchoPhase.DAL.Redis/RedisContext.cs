// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Database;
using EchoPhase.DAL.Redis.Database;
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
