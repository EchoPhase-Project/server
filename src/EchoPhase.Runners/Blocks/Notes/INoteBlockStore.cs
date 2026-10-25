// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Notes
{
    /// <summary>A note as blocks see it (and output it).</summary>
    public sealed record BlockNote(Guid Id, string Title, string Body, bool Pinned, DateTime CreatedAt, DateTime UpdatedAt)
    {
        public Dictionary<string, object?> ToOutput() => new()
        {
            ["id"] = Id.ToString(),
            ["title"] = Title,
            ["body"] = Body,
            ["pinned"] = Pinned,
            ["createdAt"] = CreatedAt,
            ["updatedAt"] = UpdatedAt,
        };
    }

    /// <summary>
    /// The notes of a workflow's owner, for the note blocks: their own and
    /// those shared with them (to edit, for changes). The host implements it.
    /// </summary>
    public interface INoteBlockStore
    {
        Task<BlockNote> CreateAsync(Guid userId, string title, string body, bool pinned, CancellationToken ct = default);

        /// <summary>A note the user may read, or null.</summary>
        Task<BlockNote?> GetAsync(Guid userId, Guid noteId, CancellationToken ct = default);

        /// <summary>The user's own note with this title (ignoring case), or null.</summary>
        Task<BlockNote?> FindByTitleAsync(Guid userId, string title, CancellationToken ct = default);

        /// <summary>Changes the note: what is null stays. Throws when it may not be changed.</summary>
        Task<BlockNote> UpdateAsync(Guid userId, Guid noteId, string? title, string? body, bool? pinned,
            CancellationToken ct = default);

        /// <summary>Adds a paragraph at the end. Throws when it may not be changed.</summary>
        Task<BlockNote> AppendAsync(Guid userId, Guid noteId, string text, CancellationToken ct = default);

        /// <summary>Whether there was a note of the user's to delete.</summary>
        Task<bool> DeleteAsync(Guid userId, Guid noteId, CancellationToken ct = default);
    }

    /// <summary>Tells a workflow's owner something (a notification). The host implements it.</summary>
    public interface IBlockNotifier
    {
        /// <summary>Returns the notification's id.</summary>
        Task<Guid> NotifyAsync(Guid userId, string title, string body, string link, CancellationToken ct = default);
    }
}
