// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using EchoPhase.Notifications;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Notifications
{
    /// <summary>
    /// The caller's inbox: what they were notified about, kept until it
    /// expires. New ones also come live over the gateway (intent
    /// notifications, event notification.created).
    /// </summary>
    [Route("api/v1/notifications")]
    public class NotificationsController : ApiControllerBase
    {
        private readonly NotificationService _notifications;

        public NotificationsController(NotificationService notifications)
        {
            _notifications = notifications;
        }

        /// <summary>The newest notifications, and how many in the inbox are unread.</summary>
        [HttpGet]
        [RequirePermission(Resources.Notifications, Permissions.Read)]
        [ProducesResponseType<NotificationInboxResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List([Range(1, 1000)] int limit = 50, CancellationToken ct = default)
        {
            var inbox = await _notifications.InboxAsync(User.GetUserId(), limit, ct);
            return Ok(new NotificationInboxResponse(inbox.Items.Select(NotificationResponse.From).ToList(), inbox.Unread));
        }

        [HttpPost("{id:guid}/read")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Notifications, Permissions.Update)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        {
            var result = await _notifications.MarkReadAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        /// <summary>Marks the whole inbox read.</summary>
        [HttpPost("read")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Notifications, Permissions.Update)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        {
            await _notifications.MarkAllReadAsync(User.GetUserId(), ct);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Notifications, Permissions.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var result = await _notifications.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }
    }
}
