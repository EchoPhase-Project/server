// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.DAL.Scylla.Entities;

namespace EchoPhase.Server.Controllers.Api.v1.Notifications
{
    /// <param name="Id">The notification's id.</param>
    /// <param name="Kind">What it is about: workflow, sign_in, storage_quota, note_shared, note_reminder; more may come.</param>
    /// <param name="Title">In English: show it for a kind you have no words for.</param>
    /// <param name="Body">More text, or empty.</param>
    /// <param name="Link">A path in the app to open, or null.</param>
    /// <param name="Params">The kind's values, to word it with (e.g. device, percent, note).</param>
    /// <param name="Read">Whether it was marked read.</param>
    /// <param name="CreatedAt">When it was sent.</param>
    public sealed record NotificationResponse(Guid Id, string Kind, string Title, string Body, string? Link,
        IReadOnlyDictionary<string, string> Params, bool Read, DateTimeOffset CreatedAt)
    {
        public static NotificationResponse From(Notification n) =>
            new(n.Id, n.Kind, n.Title, n.Body, string.IsNullOrEmpty(n.Link) ? null : n.Link, ParamsOf(n.Params), n.Read,
                n.CreatedAt);

        private static IReadOnlyDictionary<string, string> ParamsOf(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            }
            catch (JsonException)
            {
                return new Dictionary<string, string>();
            }
        }
    }

    /// <param name="Items">The newest notifications first.</param>
    /// <param name="Unread">How many in the inbox are unread.</param>
    public sealed record NotificationInboxResponse(IReadOnlyList<NotificationResponse> Items, int Unread);
}
