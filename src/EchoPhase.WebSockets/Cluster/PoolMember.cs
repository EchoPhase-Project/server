// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;

namespace EchoPhase.WebSockets.Cluster
{
    /// <summary>A connection in a shard pool, on some replica.</summary>
    /// <param name="Intents">Its intents, to pick only a member that wants the event.</param>
    public sealed record PoolMember(Guid UserId, string Pool, string ReplicaId, Guid ConnectionId, BitArray Intents)
    {
        /// <summary>Unique across the cluster: replica/connection.</summary>
        public string Id => $"{ReplicaId}/{ConnectionId:N}";
    }

    /// <summary>The member picked for one pool of one user.</summary>
    /// <param name="MemberId">Its <see cref="PoolMember.Id"/>.</param>
    public sealed record PoolPick(Guid UserId, string Pool, string MemberId);

    /// <summary>
    /// One event on its way to every replica: each sends it to its own shared
    /// connections of <see cref="Users"/> and to the pool members named in
    /// <see cref="Picks"/> that live there.
    /// </summary>
    /// <param name="Users">Whose connections; null: everyone's.</param>
    /// <param name="Intents">A connection needs all of them.</param>
    /// <param name="Payload">The message as sent to clients (UTF-8 JSON).</param>
    /// <param name="Picks">One member per pool.</param>
    /// <param name="Key">The shard key the picks were made by; a redelivery picks by it again.</param>
    /// <param name="OnlyPicks">A redelivery: to the picks only, the shared connections had it.</param>
    /// <param name="Attempt">0 first; each redelivery adds one.</param>
    public sealed record GatewayDelivery(
        Guid[]? Users,
        string[] Intents,
        byte[] Payload,
        PoolPick[] Picks,
        string Key,
        bool OnlyPicks = false,
        int Attempt = 0);
}
