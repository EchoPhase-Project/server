// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EchoPhase.WebSockets.Cluster
{
    /// <summary>
    /// Replicas sharing a Redis: deliveries over pub/sub; pool members in a
    /// hash per user (field "pool\nreplica/connection", value its intents);
    /// replicas alive by a heartbeat in a sorted set. A replica that dies
    /// without leaving stops counting once its heartbeat is stale, and its
    /// members are dropped when next read.
    /// </summary>
    public class RedisGatewayBackplane : IGatewayBackplane
    {
        /// <summary>How often a replica says it is alive.</summary>
        public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

        /// <summary>Silent for this long: taken for dead (three missed heartbeats).</summary>
        public static readonly TimeSpan DeadAfter = TimeSpan.FromSeconds(30);

        private const string Prefix = "gateway:";
        private static readonly RedisChannel Deliveries = RedisChannel.Literal(Prefix + "deliveries");
        private static readonly RedisKey Replicas = Prefix + "replicas";
        private static readonly RedisKey PooledUsers = Prefix + "pooled-users";

        private readonly Func<IConnectionMultiplexer> _redis;
        private readonly TimeProvider _time;
        private readonly ILogger<RedisGatewayBackplane> _logger;

        public RedisGatewayBackplane(Func<IConnectionMultiplexer> redis, TimeProvider time, ILogger<RedisGatewayBackplane> logger)
        {
            _redis = redis;
            _time = time;
            _logger = logger;
            // The pod name says where it runs; the suffix tells restarts apart.
            ReplicaId = $"{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}";
        }

        public string ReplicaId { get; }

        private IDatabase Db => _redis().GetDatabase();

        public async Task PublishAsync(GatewayDelivery delivery, CancellationToken ct = default)
            => await _redis().GetSubscriber().PublishAsync(Deliveries, JsonSerializer.SerializeToUtf8Bytes(delivery));

        public async Task SubscribeAsync(Func<GatewayDelivery, Task> deliver, CancellationToken ct = default)
        {
            var queue = await _redis().GetSubscriber().SubscribeAsync(Deliveries);
            // In order, one at a time, off the multiplexer's thread.
            queue.OnMessage(async message =>
            {
                try
                {
                    if (message.Message.HasValue
                        && JsonSerializer.Deserialize<GatewayDelivery>((byte[]?)message.Message ?? []) is { } delivery)
                        await deliver(delivery);
                }
                catch (Exception e) when (e is JsonException or InvalidOperationException)
                {
                    _logger.LogWarning(e, "Dropped a gateway delivery that did not read");
                }
            });
            await HeartbeatAsync(ct);
        }

        public async Task JoinAsync(WebSocketConnection connection, CancellationToken ct = default)
        {
            if (connection.ShardPool is not { } pool)
                return;
            var db = Db;
            await db.HashSetAsync(UserKey(connection.UserId), Field(pool, connection.Id), IntentBits.Write(connection.Intents));
            await db.SetAddAsync(PooledUsers, connection.UserId.ToString("N"));
        }

        public async Task LeaveAsync(WebSocketConnection connection, CancellationToken ct = default)
        {
            if (connection.ShardPool is not { } pool)
                return;
            await Db.HashDeleteAsync(UserKey(connection.UserId), Field(pool, connection.Id));
        }

        public async Task ForgetAsync(PoolMember member, CancellationToken ct = default)
            => await Db.HashDeleteAsync(UserKey(member.UserId), $"{member.Pool}\n{member.Id}");

        public async Task<IReadOnlyList<PoolMember>> MembersAsync(IReadOnlyCollection<Guid>? userIds, CancellationToken ct = default)
        {
            var db = Db;
            var users = userIds?.ToList()
                ?? (await db.SetMembersAsync(PooledUsers))
                    .Select(v => Guid.TryParseExact(v.ToString(), "N", out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToList();
            if (users.Count == 0)
                return [];

            var since = (_time.GetUtcNow() - DeadAfter).ToUnixTimeMilliseconds();
            var alive = (await db.SortedSetRangeByScoreAsync(Replicas, since))
                .Select(v => v.ToString())
                .ToHashSet(StringComparer.Ordinal);

            var members = new List<PoolMember>();
            foreach (var user in users)
            {
                var key = UserKey(user);
                var entries = await db.HashGetAllAsync(key);
                if (entries.Length == 0)
                {
                    if (userIds is null)
                        await db.SetRemoveAsync(PooledUsers, user.ToString("N"));
                    continue;
                }

                var stale = new List<RedisValue>();
                foreach (var entry in entries)
                {
                    if (Parse(user, entry.Name.ToString(), entry.Value.ToString()) is not { } member)
                        stale.Add(entry.Name);
                    else if (!alive.Contains(member.ReplicaId))
                        stale.Add(entry.Name);
                    else
                        members.Add(member);
                }
                if (stale.Count > 0)
                    await db.HashDeleteAsync(key, [.. stale]);
            }
            return members;
        }

        public async Task HeartbeatAsync(CancellationToken ct = default)
        {
            var db = Db;
            var now = _time.GetUtcNow();
            await db.SortedSetAddAsync(Replicas, ReplicaId, now.ToUnixTimeMilliseconds());
            // Replicas gone for long are forgotten (their members were dropped when read).
            await db.SortedSetRemoveRangeByScoreAsync(Replicas, double.NegativeInfinity,
                (now - TimeSpan.FromHours(1)).ToUnixTimeMilliseconds());
        }

        private static RedisKey UserKey(Guid userId) => $"{Prefix}pool:{userId:N}";

        private string Field(string pool, Guid connectionId) => $"{pool}\n{ReplicaId}/{connectionId:N}";

        // "pool\nreplica/connection" → a member; null for a field it cannot read.
        private static PoolMember? Parse(Guid userId, string field, string value)
        {
            var newline = field.IndexOf('\n');
            var slash = field.LastIndexOf('/');
            if (newline <= 0 || slash <= newline + 1
                || !Guid.TryParseExact(field[(slash + 1)..], "N", out var connectionId)
                || IntentBits.Read(value) is not { } intents)
                return null;
            return new PoolMember(userId, field[..newline], field[(newline + 1)..slash], connectionId, intents);
        }
    }
}
