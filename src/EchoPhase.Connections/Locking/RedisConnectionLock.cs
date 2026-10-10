// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using StackExchange.Redis;

namespace EchoPhase.Connections.Locking
{
    public class RedisConnectionLock : IConnectionLock
    {
        private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

        private readonly IConnectionMultiplexer _redis;
        private readonly TimeProvider _time;

        public RedisConnectionLock(IConnectionMultiplexer redis, TimeProvider time)
        {
            _redis = redis;
            _time = time;
        }

        public async Task<IAsyncDisposable> AcquireAsync(
            Guid userId, string provider, string name, CancellationToken ct = default)
        {
            var db = _redis.GetDatabase();
            RedisKey key = $"connections:lock:{userId}:{provider}:{name}";
            RedisValue owner = Guid.NewGuid().ToString();

            var deadline = _time.GetUtcNow().UtcDateTime + LockWait;
            while (!await db.LockTakeAsync(key, owner, LockExpiry))
            {
                if (_time.GetUtcNow().UtcDateTime >= deadline)
                    throw new TimeoutException($"Timed out waiting for connection lock '{key}'.");

                await Task.Delay(PollInterval, ct);
            }

            return new Releaser(db, key, owner);
        }

        private sealed class Releaser(IDatabase db, RedisKey key, RedisValue owner) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync() => await db.LockReleaseAsync(key, owner);
        }
    }
}
