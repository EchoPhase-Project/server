// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using EchoPhase.Security.BitMasks;
using Microsoft.Extensions.Logging;

namespace EchoPhase.WebSockets.Cluster
{
    /// <summary>
    /// Events to gateway connections across replicas. A connection is shared
    /// (no pool: it gets every event of its user) or in a shard pool (the pool
    /// gets each event once, on one member). Sending picks the members and
    /// publishes; every replica then delivers to its own connections. A pick
    /// that cannot be reached is forgotten and the event goes to the pool's
    /// next member for the same key.
    /// </summary>
    public class GatewayDispatcher
    {
        /// <summary>Tries per pool for one event: the first and two redeliveries.</summary>
        public const int MaxAttempts = 3;

        private readonly IGatewayBackplane _backplane;
        private readonly WebSocketConnectionManager _connections;
        private readonly IntentsBitMask _intents;
        private readonly ILogger<GatewayDispatcher> _logger;

        public GatewayDispatcher(
            IGatewayBackplane backplane,
            WebSocketConnectionManager connections,
            IntentsBitMask intents,
            ILogger<GatewayDispatcher> logger)
        {
            _backplane = backplane;
            _connections = connections;
            _intents = intents;
            _logger = logger;
        }

        /// <summary>
        /// Sends <paramref name="payload"/> to the connections of
        /// <paramref name="userIds"/> (null: everyone's) that have every one of
        /// <paramref name="requiredIntents"/>.
        /// </summary>
        /// <param name="shardKey">Picks the member in each pool: the same key, the same member while it stays; null: any.</param>
        public async Task SendAsync(IReadOnlyCollection<Guid>? userIds, byte[] payload, IReadOnlyCollection<string> requiredIntents,
            string? shardKey, CancellationToken ct = default)
        {
            if (userIds is { Count: 0 })
                return;
            var key = shardKey ?? Guid.NewGuid().ToString("N");
            var required = requiredIntents.ToArray();
            var picks = Pick(await _backplane.MembersAsync(userIds, ct), required, key, exclude: null);
            await _backplane.PublishAsync(new GatewayDelivery(userIds?.ToArray(), required, payload, picks, key), ct);
        }

        /// <summary>What this replica does with a delivery: its shared connections, and its picks.</summary>
        public async Task DeliverAsync(GatewayDelivery delivery)
        {
            var mine = _backplane.ReplicaId + "/";
            var picks = delivery.Picks
                .Where(p => p.MemberId.StartsWith(mine, StringComparison.Ordinal))
                .ToDictionary(p => p.MemberId, StringComparer.Ordinal);
            var connections = delivery.Users is null
                ? _connections.GetAllConnections()
                : delivery.Users.SelectMany(_connections.GetConnections);

            var sends = new List<Task>();
            foreach (var connection in connections)
            {
                if (connection.ShardPool is null)
                {
                    if (!delivery.OnlyPicks && Wants(connection, delivery.Intents))
                        sends.Add(TrySendAsync(connection, delivery.Payload));
                }
                else if (picks.Remove($"{mine}{connection.Id:N}", out var pick))
                {
                    sends.Add(SendPickAsync(connection, pick, delivery));
                }
            }

            // Picked here but gone already (closed between the pick and now).
            foreach (var pick in picks.Values)
                sends.Add(RedeliverAsync(delivery, pick));

            await Task.WhenAll(sends);
        }

        private async Task SendPickAsync(WebSocketConnection connection, PoolPick pick, GatewayDelivery delivery)
        {
            if (!await TrySendAsync(connection, delivery.Payload))
                await RedeliverAsync(delivery, pick);
        }

        // The same pool again, by the same key, without the member that failed.
        private async Task RedeliverAsync(GatewayDelivery delivery, PoolPick failed)
        {
            try
            {
                var members = await _backplane.MembersAsync([failed.UserId]);
                if (members.FirstOrDefault(m => m.Id == failed.MemberId) is { } gone)
                    await _backplane.ForgetAsync(gone);
                if (delivery.Attempt + 1 >= MaxAttempts)
                {
                    _logger.LogWarning("Gave up an event for shard pool {Pool} after {Attempts} tries", failed.Pool, MaxAttempts);
                    return;
                }

                var next = Pick(members.Where(m => m.Pool == failed.Pool), delivery.Intents, delivery.Key,
                    new HashSet<string>(StringComparer.Ordinal) { failed.MemberId });
                if (next.Length == 0)
                    return;
                await _backplane.PublishAsync(delivery with
                {
                    Users = [failed.UserId],
                    Picks = next,
                    OnlyPicks = true,
                    Attempt = delivery.Attempt + 1,
                });
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger.LogWarning(e, "Could not redeliver an event for shard pool {Pool}", failed.Pool);
            }
        }

        // One member per (user, pool) among those with the intents.
        private PoolPick[] Pick(IEnumerable<PoolMember> members, string[] required, string key, IReadOnlySet<string>? exclude)
            => members
                .Where(m => _intents.Has(m.Intents, required))
                .GroupBy(m => (m.UserId, m.Pool))
                .Select(pool => PoolRouting.Pick(pool, key, exclude))
                .OfType<PoolMember>()
                .Select(m => new PoolPick(m.UserId, m.Pool, m.Id))
                .ToArray();

        private bool Wants(WebSocketConnection connection, string[] required)
            => connection.IsOpen && _intents.Has(connection.Intents, required);

        private async Task<bool> TrySendAsync(WebSocketConnection connection, byte[] payload)
        {
            if (!connection.IsOpen)
                return false;
            try
            {
                await connection.SendAsync(payload, CancellationToken.None);
                return connection.IsOpen;
            }
            catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or OperationCanceledException)
            {
                _logger.LogDebug(ex, "Could not send to connection {ConnectionId}", connection.Id);
                return false;
            }
        }
    }
}
