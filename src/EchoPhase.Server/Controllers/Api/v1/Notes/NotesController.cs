// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Postgres.Configurations;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Notes;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Notes
{
    /// <param name="Id">The user's id.</param>
    /// <param name="UserName">Their username.</param>
    /// <param name="Name">Their display name.</param>
    public sealed record NoteUserResponse(Guid Id, string? UserName, string Name)
    {
        public static NoteUserResponse From(User? user, Guid id) => new(id, user?.UserName, user?.Name ?? string.Empty);
    }

    /// <param name="Id">The note's id.</param>
    /// <param name="Title">Its title.</param>
    /// <param name="Body">Markdown; render it without raw HTML.</param>
    /// <param name="Pinned">Kept on top of its owner's list.</param>
    /// <param name="RemindAt">When its owner is reminded (a notification), or null.</param>
    /// <param name="Access">What the caller may do with it: Owner, Edit or Read.</param>
    /// <param name="Owner">Whose it is.</param>
    /// <param name="CreatedAt">When it was created.</param>
    /// <param name="UpdatedAt">When it last changed.</param>
    public sealed record NoteResponse(Guid Id, string Title, string Body, bool Pinned, DateTime? RemindAt,
        NoteAccess Access, NoteUserResponse Owner, DateTime CreatedAt, DateTime UpdatedAt)
    {
        public static NoteResponse From(NoteView view)
        {
            var note = view.Note;
            var mine = view.Access == NoteAccess.Owner;
            return new(note.Id, note.Title, note.Body, mine && note.Pinned, mine ? note.RemindAt : null, view.Access,
                NoteUserResponse.From(note.Owner, note.OwnerId), note.CreatedAt, note.UpdatedAt);
        }
    }

    /// <param name="Title">Empty: the text's first line.</param>
    /// <param name="Body">Markdown.</param>
    /// <param name="Pinned">Keep it on top of the list (the owner's choice).</param>
    /// <param name="RemindAt">When to be reminded, with Z or an offset; null: no reminder (the owner's choice).</param>
    public sealed record NoteRequest(
        [MaxLength(NoteConfiguration.MaxTitleLength)] string? Title,
        [MaxLength(NoteConfiguration.MaxBodyLength)] string? Body,
        bool Pinned = false,
        DateTimeOffset? RemindAt = null)
    {
        public NoteInput ToInput() => new(Title, Body, Pinned, RemindAt);
    }

    /// <param name="UserName">Whom to share it with.</param>
    /// <param name="CanEdit">May change the title and text, not only read.</param>
    public sealed record ShareNoteRequest([Required] string UserName, bool CanEdit = false);

    /// <param name="User">Whom it is shared with.</param>
    /// <param name="CanEdit">May change the title and text.</param>
    /// <param name="CreatedAt">Since when.</param>
    public sealed record NoteShareResponse(NoteUserResponse User, bool CanEdit, DateTime CreatedAt)
    {
        public static NoteShareResponse From(NoteShare share) =>
            new(NoteUserResponse.From(share.User, share.UserId), share.CanEdit, share.CreatedAt);
    }

    /// <summary>
    /// The caller's notes (Markdown) and those shared with them. A note the
    /// caller may not read answers 404; one they may read but not change, 403.
    /// </summary>
    [Route("api/v1/notes")]
    public class NotesController : ApiControllerBase
    {
        private readonly NoteService _notes;

        public NotesController(NoteService notes)
        {
            _notes = notes;
        }

        /// <summary>The caller's notes and those shared with them: their pinned ones first, then by last change. `q` searches title and text.</summary>
        [HttpGet]
        [RequirePermission(Resources.Notes, Permissions.Read)]
        [ProducesResponseType<NoteResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List([MaxLength(200)] string? q, [Range(1, 500)] int limit = 100,
            CancellationToken ct = default)
        {
            var notes = await _notes.ListAsync(User.GetUserId(), q, limit, ct);
            return Ok(notes.Select(NoteResponse.From));
        }

        [HttpPost]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Notes, Permissions.Create)]
        [ProducesResponseType<NoteResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] NoteRequest dto, CancellationToken ct)
        {
            var result = await _notes.CreateAsync(User.GetUserId(), dto.ToInput(), ct);
            if (!result.Successful)
                return Failure(result.Error);
            return CreatedAtAction(nameof(Get), new { id = result.Value!.Note.Id }, NoteResponse.From(result.Value));
        }

        [HttpGet("{id:guid}")]
        [RequirePermission(Resources.Notes, Permissions.Read)]
        [ProducesResponseType<NoteResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var result = await _notes.GetAsync(User.GetUserId(), id, ct);
            return result.Successful ? Ok(NoteResponse.From(result.Value!)) : Failure(result.Error);
        }

        /// <summary>Replaces the note's content. Pinned and RemindAt are the owner's: from others they are ignored.</summary>
        [HttpPut("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Notes, Permissions.Update)]
        [ProducesResponseType<NoteResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] NoteRequest dto, CancellationToken ct)
        {
            var result = await _notes.UpdateAsync(User.GetUserId(), id, dto.ToInput(), ct);
            return result.Successful ? Ok(NoteResponse.From(result.Value!)) : Failure(result.Error);
        }

        /// <summary>Deletes the note: only its owner may.</summary>
        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Notes, Permissions.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var result = await _notes.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        /// <summary>Whom the note is shared with: only its owner may see.</summary>
        [HttpGet("{id:guid}/shares")]
        [RequirePermission(Resources.Notes, Permissions.Read)]
        [ProducesResponseType<NoteShareResponse[]>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Shares(Guid id, CancellationToken ct)
        {
            var result = await _notes.SharesAsync(User.GetUserId(), id, ct);
            return result.Successful ? Ok(result.Value!.Select(NoteShareResponse.From)) : Failure(result.Error);
        }

        /// <summary>Shares the note with a user, or changes what they may do; they are notified when it is new to them.</summary>
        [HttpPut("{id:guid}/shares")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Notes, Permissions.Update)]
        [ProducesResponseType<NoteShareResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Share(Guid id, [FromBody] ShareNoteRequest dto, CancellationToken ct)
        {
            var result = await _notes.ShareAsync(User.GetUserId(), id, dto.UserName, dto.CanEdit, ct);
            return result.Successful ? Ok(NoteShareResponse.From(result.Value!)) : Failure(result.Error);
        }

        /// <summary>Stops sharing the note with a user: its owner may, and so may that user (to leave it).</summary>
        [HttpDelete("{id:guid}/shares/{userId:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Notes, Permissions.Update)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unshare(Guid id, Guid userId, CancellationToken ct)
        {
            var result = await _notes.UnshareAsync(User.GetUserId(), id, userId, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }
    }
}
