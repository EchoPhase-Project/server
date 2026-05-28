// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;

namespace EchoPhase.Clients.Discord.WebSocket
{
    /// <summary>
    /// Implemented by any component that wants to receive Discord Gateway payloads.
    /// Implement <see cref="IDisposable"/> to automatically unsubscribe on deconstruction.
    /// </summary>
    public interface IDiscordWebSocketSubscriber : IDisposable
    {
        /// <summary>
        /// Called by <see cref="DiscordWebSocketManager"/> whenever a payload
        /// arrives on any connection that belongs to the subscribed user.
        /// </summary>
        /// <param name="userId">The user whose connection produced the payload.</param>
        /// <param name="tokenName">The token / connection name the payload arrived on.</param>
        /// <param name="connectionId">The exact connection ID.</param>
        /// <param name="payload">The raw Gateway JSON payload.</param>
        Task OnPayloadAsync(Guid userId, string tokenName, Guid connectionId, JsonDocument payload);
    }
}
