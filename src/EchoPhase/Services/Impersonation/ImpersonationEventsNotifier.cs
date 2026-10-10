// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Interfaces.Services;
using EchoPhase.Security.Authentication.Impersonation;
using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Services.Impersonation
{
    /// <summary>
    /// Tells the target, over their websockets and webhooks, that someone is acting as them.
    /// </summary>
    public class ImpersonationEventsNotifier : IImpersonationEvents
    {
        private readonly IEventService _events;

        public ImpersonationEventsNotifier(IEventService events)
        {
            _events = events;
        }

        public Task StartedAsync(Guid targetId, ImpersonationNotice notice, CancellationToken ct = default)
            => _events.SendMessageToUsersAsync(
                new HashSet<Guid> { targetId },
                new { type = "impersonation.started", impersonation = notice },
                new HashSet<string> { Intents.Impersonation },
                ct);
    }
}
