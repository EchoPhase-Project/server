// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Notes
{
    /// <summary>What a user may do with a note.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<NoteAccess>))]
    public enum NoteAccess
    {
        /// <summary>Shared to read.</summary>
        Read,
        /// <summary>Shared to edit: the title and text.</summary>
        Edit,
        /// <summary>Theirs: everything, sharing and deleting included.</summary>
        Owner,
    }

    /// <summary>A note as one user sees it.</summary>
    public sealed record NoteView(Note Note, NoteAccess Access)
    {
        public bool CanEdit => Access != NoteAccess.Read;
    }

    /// <summary>A note's content as given; the title may be empty (taken from the text then).</summary>
    public sealed record NoteInput(string? Title, string? Body, bool Pinned = false, DateTimeOffset? RemindAt = null);
}
