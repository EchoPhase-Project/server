// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections.Models;
using EchoPhase.Connections.Services;
using EchoPhase.Interfaces.Services;
using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Services.Connections
{
    /// <summary>
    /// Delivers connection events over the user's websockets and webhooks.
    /// </summary>
    public class ConnectionEventsNotifier : IConnectionEvents
    {
        private readonly IEventService _events;

        public ConnectionEventsNotifier(IEventService events)
        {
            _events = events;
        }

        public Task ReauthRequiredAsync(Guid userId, ConnectionSummary connection, CancellationToken ct = default)
            => _events.SendMessageToUsersAsync(
                new HashSet<Guid> { userId },
                new { type = "connection.reauth_required", connection },
                new HashSet<string> { Intents.Connections },
                ct);
    }
}
