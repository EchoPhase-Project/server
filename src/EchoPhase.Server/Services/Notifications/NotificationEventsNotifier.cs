// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Files;
using EchoPhase.Notes;
using EchoPhase.Notifications;
using EchoPhase.Runners.Blocks.Notes;

namespace EchoPhase.Server.Services.Notifications
{
    /// <summary>
    /// Turns what notes, files and workflows have to say into notifications,
    /// with the values clients word each kind with.
    /// </summary>
    public class NotificationEventsNotifier : INoteEvents, IFileEvents, IBlockNotifier
    {
        private readonly NotificationService _notifications;

        public NotificationEventsNotifier(NotificationService notifications)
        {
            _notifications = notifications;
        }

        public Task SharedAsync(Note note, Guid userId, string sharedBy, bool canEdit, CancellationToken ct = default)
            => _notifications.SendAsync(userId, new NotificationMessage(
                NotificationKinds.NoteShared,
                $"{sharedBy} shared a note with you",
                note.Title,
                $"/notes?open={note.Id}",
                new Dictionary<string, string>
                {
                    ["note"] = note.Title,
                    ["by"] = sharedBy,
                    ["access"] = canEdit ? "edit" : "read",
                }), ct);

        public Task ReminderAsync(Note note, CancellationToken ct = default)
            => _notifications.SendAsync(note.OwnerId, new NotificationMessage(
                NotificationKinds.NoteReminder,
                $"Reminder: {note.Title}",
                string.Empty,
                $"/notes?open={note.Id}",
                new Dictionary<string, string> { ["note"] = note.Title }), ct);

        public Task QuotaNearlyFullAsync(Guid ownerId, int percent, CancellationToken ct = default)
            => _notifications.SendAsync(ownerId, new NotificationMessage(
                NotificationKinds.StorageQuota,
                $"Your files take {percent}% of your storage",
                "Delete files you no longer need to keep uploading.",
                "/files",
                new Dictionary<string, string> { ["percent"] = percent.ToString(System.Globalization.CultureInfo.InvariantCulture) }), ct);

        public async Task<Guid> NotifyAsync(Guid userId, string title, string body, string link, CancellationToken ct = default)
            => (await _notifications.SendAsync(userId,
                new NotificationMessage(NotificationKinds.Workflow, title, body, link), ct)).Id;

        /// <summary>Someone signed in to the user's account, from this device (its user agent).</summary>
        public Task SignedInAsync(Guid userId, string device, CancellationToken ct = default)
            => _notifications.SendAsync(userId, new NotificationMessage(
                NotificationKinds.SignIn,
                "New sign-in to your account",
                device,
                "/sessions",
                new Dictionary<string, string> { ["device"] = device }), ct);
    }
}
