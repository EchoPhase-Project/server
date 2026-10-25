// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Server.Interfaces.Services
{
    /// <summary>
    /// Events to users' gateway connections (every replica: shared ones all,
    /// one member per shard pool) and to their webhooks.
    /// </summary>
    public interface IEventService
    {
        /// <param name="shardKey">Picks the member in each shard pool: the same key, the same member while it stays; null: any.</param>
        Task SendMessageToAllAsync<T>(T message, HashSet<string> intents, CancellationToken ct = default, string? shardKey = null);

        /// <inheritdoc cref="SendMessageToAllAsync"/>
        Task SendMessageToUsersAsync<T>(HashSet<Guid> userIds, T message, HashSet<string> intents, CancellationToken ct = default,
            string? shardKey = null);

        /// <inheritdoc cref="SendMessageToAllAsync"/>
        Task SendMessageToRolesAsync<T>(HashSet<string> roles, T message, HashSet<string> intents, CancellationToken ct = default,
            string? shardKey = null);
    }
}
