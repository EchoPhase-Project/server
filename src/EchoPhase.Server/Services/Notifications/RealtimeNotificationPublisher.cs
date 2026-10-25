// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Notifications;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Notifications;
using EchoPhase.Server.Interfaces.Services;

namespace EchoPhase.Server.Services.Notifications
{
    /// <summary>
    /// Delivers notifications live: to the user's websockets subscribed to
    /// the notifications intent (and their webhooks), as notification.created.
    /// </summary>
    public class RealtimeNotificationPublisher : INotificationPublisher
    {
        private readonly IEventService _events;

        public RealtimeNotificationPublisher(IEventService events)
        {
            _events = events;
        }

        public Task PublishAsync(Notification notification, CancellationToken ct = default)
            => _events.SendMessageToUsersAsync(
                new HashSet<Guid> { notification.UserId },
                new { type = "notification.created", notification = NotificationResponse.From(notification) },
                new HashSet<string> { Intents.Notifications },
                ct);
    }
}
