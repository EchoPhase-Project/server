// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.WebSockets.Cluster
{
    /// <summary>
    /// What gateway replicas share: deliveries reach every replica, and the
    /// members of every shard pool are known to all of them.
    /// </summary>
    public interface IGatewayBackplane
    {
        /// <summary>This replica's id: the first part of its members' ids.</summary>
        string ReplicaId { get; }

        /// <summary>Hands a delivery to every replica, this one included.</summary>
        Task PublishAsync(GatewayDelivery delivery, CancellationToken ct = default);

        /// <summary>Starts receiving deliveries (once, from the hosted service).</summary>
        Task SubscribeAsync(Func<GatewayDelivery, Task> deliver, CancellationToken ct = default);

        /// <summary>Lists (or updates: intents) a connection as its pool's member; nothing without a pool.</summary>
        Task JoinAsync(WebSocketConnection connection, CancellationToken ct = default);

        /// <summary>Takes a connection out of its pool.</summary>
        Task LeaveAsync(WebSocketConnection connection, CancellationToken ct = default);

        /// <summary>Forgets a member that could not be reached.</summary>
        Task ForgetAsync(PoolMember member, CancellationToken ct = default);

        /// <summary>The live members of these users' pools; null: every user's.</summary>
        Task<IReadOnlyList<PoolMember>> MembersAsync(IReadOnlyCollection<Guid>? userIds, CancellationToken ct = default);

        /// <summary>Tells the others this replica is alive (called periodically).</summary>
        Task HeartbeatAsync(CancellationToken ct = default);
    }
}
