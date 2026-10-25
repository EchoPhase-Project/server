// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Notes;
using EchoPhase.Runners.Blocks.Notes;
using EchoPhase.Types.Result;

namespace EchoPhase.Server.Services.Notes
{
    /// <summary>The note blocks' view of <see cref="NoteService"/>: the same rules as the API.</summary>
    public sealed class NoteBlockStore : INoteBlockStore
    {
        private readonly NoteService _notes;

        public NoteBlockStore(NoteService notes)
        {
            _notes = notes;
        }

        public async Task<BlockNote> CreateAsync(Guid userId, string title, string body, bool pinned, CancellationToken ct = default)
            => Of(Require(await _notes.CreateAsync(userId, new NoteInput(title, body, pinned), ct)).Note);

        public async Task<BlockNote?> GetAsync(Guid userId, Guid noteId, CancellationToken ct = default)
        {
            var result = await _notes.GetAsync(userId, noteId, ct);
            return result.Successful ? Of(result.Value.Note) : null;
        }

        public async Task<BlockNote?> FindByTitleAsync(Guid userId, string title, CancellationToken ct = default)
            => await _notes.FindByTitleAsync(userId, title, ct) is { } note ? Of(note) : null;

        public async Task<BlockNote> UpdateAsync(Guid userId, Guid noteId, string? title, string? body, bool? pinned,
            CancellationToken ct = default)
        {
            var current = Require(await _notes.GetAsync(userId, noteId, ct)).Note;
            var input = new NoteInput(title ?? current.Title, body ?? current.Body, pinned ?? current.Pinned,
                current.RemindAt is { } remind ? new DateTimeOffset(DateTime.SpecifyKind(remind, DateTimeKind.Utc)) : null);
            return Of(Require(await _notes.UpdateAsync(userId, noteId, input, ct)).Note);
        }

        public async Task<BlockNote> AppendAsync(Guid userId, Guid noteId, string text, CancellationToken ct = default)
            => Of(Require(await _notes.AppendAsync(userId, noteId, text, ct)).Note);

        public async Task<bool> DeleteAsync(Guid userId, Guid noteId, CancellationToken ct = default)
        {
            var result = await _notes.DeleteAsync(userId, noteId, ct);
            if (result.Successful)
                return true;
            return result.Error.Code == NoteErrors.NotFound ? false : throw new InvalidOperationException(result.Error.Message);
        }

        private static BlockNote Of(Note note) =>
            new(note.Id, note.Title, note.Body, note.Pinned, note.CreatedAt, note.UpdatedAt);

        // A refusal fails the block with the service's own words.
        private static T Require<T>(IServiceResult<T> result) =>
            result.Successful ? result.Value : throw new InvalidOperationException(result.Error.Message);
    }
}
