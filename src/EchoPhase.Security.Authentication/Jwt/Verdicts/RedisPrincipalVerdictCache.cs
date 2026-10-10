// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using EchoPhase.Configuration.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EchoPhase.Security.Authentication.Jwt.Verdicts
{
    /// <summary>
    /// One Redis hash per user (<c>auth:verdicts:{userId}</c>): field = hashed
    /// token key, value = verdict expiry in unix ms. A lookup is one HGET and
    /// invalidating a user is one DEL. Redis errors fall back to the database.
    /// </summary>
    /// <remarks>
    /// A check that read the database just before a revocation can still write
    /// its verdict just after the DEL; that token then lives at most one more TTL,
    /// the same bound the cache accepts anyway.
    /// </remarks>
    public class RedisPrincipalVerdictCache : IPrincipalVerdictCache
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisPrincipalVerdictCache> _logger;
        private readonly TimeSpan _ttl;
        private readonly TimeProvider _time;

        public RedisPrincipalVerdictCache(
            IConnectionMultiplexer redis,
            IOptions<AuthenticationOptions> options,
            ILogger<RedisPrincipalVerdictCache> logger,
            TimeProvider time)
        {
            _redis = redis;
            _logger = logger;
            _ttl = options.Value.Bearer.VerdictCacheTtl;
            _time = time;
        }

        private bool Enabled => _ttl > TimeSpan.Zero;

        private static RedisKey KeyFor(Guid userId) => $"auth:verdicts:{userId}";

        public async Task<bool> IsKnownValidAsync(ClaimsPrincipal principal)
        {
            if (!Enabled || PrincipalVerdictKey.UserId(principal) is not { } userId)
                return false;

            try
            {
                var value = await _redis.GetDatabase()
                    .HashGetAsync(KeyFor(userId), PrincipalVerdictKey.Hashed(principal));

                return value.TryParse(out long expiresAt)
                    && expiresAt > _time.GetUtcNow().ToUnixTimeMilliseconds();
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Verdict cache read failed; checking the database.");
                return false;
            }
        }

        public async Task RememberValidAsync(ClaimsPrincipal principal)
        {
            if (!Enabled || PrincipalVerdictKey.UserId(principal) is not { } userId)
                return;

            var key = KeyFor(userId);
            var expiresAt = _time.GetUtcNow().Add(_ttl).ToUnixTimeMilliseconds();

            try
            {
                // Fields carry their own expiry; the key TTL only garbage-collects
                // the hash once the user goes quiet.
                var batch = _redis.GetDatabase().CreateBatch();
                var set = batch.HashSetAsync(key, PrincipalVerdictKey.Hashed(principal), expiresAt);
                var expire = batch.KeyExpireAsync(key, _ttl);
                batch.Execute();
                await Task.WhenAll(set, expire);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Verdict cache write failed.");
            }
        }

        public async Task InvalidateAsync(IEnumerable<Guid> userIds)
        {
            var keys = userIds.Distinct().Select(KeyFor).ToArray();
            if (keys.Length == 0)
                return;

            try
            {
                await _redis.GetDatabase().KeyDeleteAsync(keys);
            }
            catch (RedisException ex)
            {
                // Entries expire on their own within one TTL.
                _logger.LogError(ex, "Verdict cache invalidation failed for {Count} user(s).", keys.Length);
            }
        }
    }
}
