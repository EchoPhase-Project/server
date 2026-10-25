// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A user's note: a title and Markdown text, readable or editable by the
    /// users it is shared with (<see cref="NoteShare"/>). Goes with its owner.
    /// </summary>
    public class Note : ITrackingEntity, IIdentifiable
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);

        public Guid OwnerId { get; set; }
        public User? Owner { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>Markdown, rendered by the client (raw HTML is not).</summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>Kept on top of the owner's list.</summary>
        public bool Pinned { get; set; }

        /// <summary>When to remind the owner (a notification); cleared once sent.</summary>
        public DateTime? RemindAt { get; set; }

        public List<NoteShare> Shares { get; set; } = [];

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A note shared with a user: to read, or to edit as well.</summary>
    public class NoteShare
    {
        public Guid NoteId { get; set; }
        public Note? Note { get; set; }

        public Guid UserId { get; set; }
        public User? User { get; set; }

        /// <summary>May change the title and text (not delete or share it).</summary>
        public bool CanEdit { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
