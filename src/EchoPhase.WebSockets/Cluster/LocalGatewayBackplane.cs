// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.WebSockets.Cluster
{
    /// <summary>
    /// One replica alone: deliveries go straight to it, and pool members are
    /// its own connections. For tests and a server without Redis.
    /// </summary>
    public class LocalGatewayBackplane : IGatewayBackplane
    {
        private readonly WebSocketConnectionManager _connections;
        private Func<GatewayDelivery, Task>? _deliver;

        public LocalGatewayBackplane(WebSocketConnectionManager connections)
        {
            _connections = connections;
        }

        public string ReplicaId => "local";

        public Task PublishAsync(GatewayDelivery delivery, CancellationToken ct = default)
            => _deliver is { } deliver ? deliver(delivery) : Task.CompletedTask;

        public Task SubscribeAsync(Func<GatewayDelivery, Task> deliver, CancellationToken ct = default)
        {
            _deliver = deliver;
            return Task.CompletedTask;
        }

        // Members are read from the connections themselves.
        public Task JoinAsync(WebSocketConnection connection, CancellationToken ct = default) => Task.CompletedTask;

        public Task LeaveAsync(WebSocketConnection connection, CancellationToken ct = default) => Task.CompletedTask;

        public Task ForgetAsync(PoolMember member, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PoolMember>> MembersAsync(IReadOnlyCollection<Guid>? userIds, CancellationToken ct = default)
        {
            var connections = userIds is null
                ? _connections.GetAllConnections()
                : userIds.SelectMany(_connections.GetConnections);
            IReadOnlyList<PoolMember> members = connections
                .Where(c => c.IsOpen && c.ShardPool is not null)
                .Select(c => new PoolMember(c.UserId, c.ShardPool ?? string.Empty, ReplicaId, c.Id, c.Intents))
                .ToList();
            return Task.FromResult(members);
        }

        public Task HeartbeatAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
