// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Configuration.Notifications;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Types.Result;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EchoPhase.Notifications
{
    /// <summary>A user's inbox and its unread count.</summary>
    public sealed record NotificationInbox(IReadOnlyList<Notification> Items, int Unread);

    /// <summary>
    /// Tells users about things: each notification is kept in their inbox
    /// until it expires, and delivered live to their open connections. What
    /// they miss while away waits there for them.
    /// </summary>
    public class NotificationService
    {
        private readonly INotificationStore _store;
        private readonly INotificationPublisher _publisher;
        private readonly NotificationsOptions _options;
        private readonly TimeProvider _time;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(INotificationStore store, INotificationPublisher publisher,
            IOptions<NotificationsOptions> options, TimeProvider time, ILogger<NotificationService> logger)
        {
            _store = store;
            _publisher = publisher;
            _options = options.Value;
            _time = time;
            _logger = logger;
        }

        /// <summary>
        /// Keeps the notification and delivers it live. Text past its limit is
        /// cut, not refused: a notification is never worth failing a workflow.
        /// </summary>
        public async Task<Notification> SendAsync(Guid userId, NotificationMessage message, CancellationToken ct = default)
        {
            var now = _time.GetUtcNow();
            var notification = new Notification
            {
                UserId = userId,
                Id = Guid.CreateVersion7(now),
                Kind = message.Kind,
                Title = Cut(message.Title, NotificationMessage.MaxTitleLength),
                Body = Cut(message.Body, NotificationMessage.MaxBodyLength),
                Link = SafeLink(message.Link),
                Params = JsonSerializer.Serialize(message.Params ?? new Dictionary<string, string>()),
                CreatedAt = now,
            };
            await _store.SaveAsync(notification, _options.Ttl, ct);

            try
            {
                await _publisher.PublishAsync(notification, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Kept already: the user sees it in the inbox, only later.
                _logger.LogWarning(e, "Could not deliver notification {Id} live", notification.Id);
            }

            return notification;
        }

        /// <summary>The user's newest notifications and how many of them are unread.</summary>
        public async Task<NotificationInbox> InboxAsync(Guid userId, int limit, CancellationToken ct = default)
        {
            var items = await _store.ListAsync(userId, _options.InboxSize, ct);
            return new NotificationInbox(items.Take(Math.Clamp(limit, 1, _options.InboxSize)).ToList(),
                items.Count(n => !n.Read));
        }

        public async Task<IServiceResult> MarkReadAsync(Guid userId, Guid id, CancellationToken ct = default)
        {
            var notification = await _store.FindAsync(userId, id, ct);
            if (notification is null)
                return NotFound();
            await MarkAsync(notification, ct);
            return ServiceResult.Success();
        }

        /// <summary>Marks every notification of the inbox read; returns how many were not.</summary>
        public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
        {
            var unread = (await _store.ListAsync(userId, _options.InboxSize, ct)).Where(n => !n.Read).ToList();
            foreach (var notification in unread)
                await MarkAsync(notification, ct);
            return unread.Count;
        }

        public async Task<IServiceResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
        {
            var notification = await _store.FindAsync(userId, id, ct);
            if (notification is null)
                return NotFound();
            await _store.DeleteAsync(notification, ct);
            return ServiceResult.Success();
        }

        // Rewritten with the time it has left, so reading does not extend its life.
        private async Task MarkAsync(Notification notification, CancellationToken ct)
        {
            if (notification.Read)
                return;
            var left = notification.CreatedAt + _options.Ttl - _time.GetUtcNow();
            if (left < TimeSpan.FromSeconds(1))
                return;
            notification.Read = true;
            await _store.SaveAsync(notification, left, ct);
        }

        private static string Cut(string? text, int max)
        {
            var value = (text ?? string.Empty).Trim();
            return value.Length <= max ? value : value[..(max - 1)] + "…";
        }

        // Only a path inside the app: a workflow must not send people elsewhere.
        private static string SafeLink(string? link)
        {
            var value = (link ?? string.Empty).Trim();
            return value.Length <= NotificationMessage.MaxLinkLength && value.StartsWith('/') && !value.StartsWith("//")
                   && !value.Contains('\\')
                ? value
                : string.Empty;
        }

        private static IServiceResult NotFound() =>
            ServiceResult.Failure(new ServiceError(NotificationErrors.NotFound, "Notification not found."));
    }
}
