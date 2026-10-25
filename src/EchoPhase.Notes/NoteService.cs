// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Configurations;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Types.Result;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Notes
{
    /// <summary>
    /// Users' notes: Markdown with a title, theirs or shared with them to read
    /// or edit. A note someone may not read does not exist for them (404);
    /// one they may read but not change answers 403.
    /// </summary>
    public class NoteService
    {
        /// <summary>The longest title made from a note's first line.</summary>
        public const int DerivedTitleLength = 80;

        private readonly PostgresContext _db;
        private readonly INoteEvents _events;
        private readonly TimeProvider _time;
        private readonly ILogger<NoteService> _logger;

        public NoteService(PostgresContext db, INoteEvents events, TimeProvider time, ILogger<NoteService> logger)
        {
            _db = db;
            _events = events;
            _time = time;
            _logger = logger;
        }

        public async Task<IServiceResult<NoteView>> CreateAsync(Guid ownerId, NoteInput input, CancellationToken ct = default)
        {
            var problem = Check(input);
            if (problem is not null)
                return Fail<NoteView>(NoteErrors.Invalid, problem.Value.Message, problem.Value.Field);

            var now = Now();
            var note = new Note
            {
                OwnerId = ownerId,
                Title = TitleOf(input),
                Body = input.Body ?? string.Empty,
                Pinned = input.Pinned,
                RemindAt = input.RemindAt?.UtcDateTime,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.Notes.Add(note);
            await _db.SaveChangesAsync(ct);
            return ServiceResult<NoteView>.Success(new NoteView(note, NoteAccess.Owner));
        }

        public async Task<IServiceResult<NoteView>> GetAsync(Guid userId, Guid noteId, CancellationToken ct = default)
        {
            var view = await FindAsync(userId, noteId, tracking: false, ct);
            return view is null ? NotFound<NoteView>() : ServiceResult<NoteView>.Success(view);
        }

        /// <summary>
        /// The user's notes and those shared with them: their pinned ones
        /// first, then the most recently changed. <paramref name="query"/>
        /// matches the title or text, ignoring case.
        /// </summary>
        public async Task<IReadOnlyList<NoteView>> ListAsync(Guid userId, string? query, int limit, CancellationToken ct = default)
        {
            var notes = _db.Notes.AsNoTracking()
                .Where(n => n.OwnerId == userId || n.Shares.Any(s => s.UserId == userId));

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim().ToLower();
                notes = notes.Where(n => n.Title.ToLower().Contains(q) || n.Body.ToLower().Contains(q));
            }

            var rows = await notes
                .OrderByDescending(n => n.OwnerId == userId && n.Pinned)
                .ThenByDescending(n => n.UpdatedAt)
                .Take(Math.Clamp(limit, 1, 500))
                .Select(n => new { Note = n, n.Owner, Share = n.Shares.FirstOrDefault(s => s.UserId == userId) })
                .ToListAsync(ct);

            return rows.Select(r =>
            {
                r.Note.Owner = r.Owner;
                return new NoteView(r.Note, AccessOf(r.Note, userId, r.Share));
            }).ToList();
        }

        /// <summary>
        /// Changes the note. Its owner changes everything; someone it is
        /// shared with to edit, the title and text (pin and reminder stay).
        /// </summary>
        public async Task<IServiceResult<NoteView>> UpdateAsync(Guid userId, Guid noteId, NoteInput input, CancellationToken ct = default)
        {
            var problem = Check(input);
            if (problem is not null)
                return Fail<NoteView>(NoteErrors.Invalid, problem.Value.Message, problem.Value.Field);

            var view = await FindAsync(userId, noteId, tracking: true, ct);
            if (view is null)
                return NotFound<NoteView>();
            if (!view.CanEdit)
                return Fail<NoteView>(NoteErrors.Forbidden, "This note is shared with you to read only.");

            var note = view.Note;
            if (input.ExpectedStamp is { } expected && expected != note.ConcurrencyStamp)
                return Fail<NoteView>(NoteErrors.Conflict, ChangedMeanwhile);
            note.Title = TitleOf(input);
            note.Body = input.Body ?? string.Empty;
            if (view.Access == NoteAccess.Owner)
            {
                note.Pinned = input.Pinned;
                note.RemindAt = input.RemindAt?.UtcDateTime;
            }
            note.UpdatedAt = Now();
            if (!await TrySaveAsync(ct))
                return Fail<NoteView>(NoteErrors.Conflict, ChangedMeanwhile);
            return ServiceResult<NoteView>.Success(view);
        }

        /// <summary>Adds text to the end of the note (a new paragraph), for workflows that keep a log.</summary>
        public async Task<IServiceResult<NoteView>> AppendAsync(Guid userId, Guid noteId, string text, CancellationToken ct = default)
        {
            var view = await FindAsync(userId, noteId, tracking: true, ct);
            if (view is null)
                return NotFound<NoteView>();
            if (!view.CanEdit)
                return Fail<NoteView>(NoteErrors.Forbidden, "This note is shared with you to read only.");

            var note = view.Note;
            var body = note.Body.Length == 0 ? text : $"{note.Body.TrimEnd()}\n\n{text}";
            if (body.Length > NoteConfiguration.MaxBodyLength)
                return Fail<NoteView>(NoteErrors.Invalid, TooLong, "body");
            note.Body = body;
            note.UpdatedAt = Now();
            if (!await TrySaveAsync(ct))
                return Fail<NoteView>(NoteErrors.Conflict, ChangedMeanwhile);
            return ServiceResult<NoteView>.Success(view);
        }

        /// <summary>Deletes the note: only its owner may.</summary>
        public async Task<IServiceResult> DeleteAsync(Guid userId, Guid noteId, CancellationToken ct = default)
        {
            var view = await FindAsync(userId, noteId, tracking: true, ct);
            if (view is null)
                return ServiceResult.Failure(new ServiceError(NoteErrors.NotFound, "Note not found."));
            if (view.Access != NoteAccess.Owner)
                return ServiceResult.Failure(new ServiceError(NoteErrors.Forbidden, "Only the note's owner may delete it."));

            _db.Notes.Remove(view.Note);
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        /// <summary>The users the note is shared with: only its owner may see them.</summary>
        public async Task<IServiceResult<IReadOnlyList<NoteShare>>> SharesAsync(Guid userId, Guid noteId, CancellationToken ct = default)
        {
            var owned = await OwnedAsync(userId, noteId, ct);
            if (owned is not null)
                return ServiceResult<IReadOnlyList<NoteShare>>.Failure(owned);

            IReadOnlyList<NoteShare> shares = await _db.NoteShares.AsNoTracking()
                .Where(s => s.NoteId == noteId)
                .Include(s => s.User)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync(ct);
            return ServiceResult<IReadOnlyList<NoteShare>>.Success(shares);
        }

        /// <summary>
        /// Shares the note with a user (by username), or changes what they may
        /// do with it. They are told when it is new to them.
        /// </summary>
        public async Task<IServiceResult<NoteShare>> ShareAsync(Guid userId, Guid noteId, string userName, bool canEdit,
            CancellationToken ct = default)
        {
            var owned = await OwnedAsync(userId, noteId, ct);
            if (owned is not null)
                return ServiceResult<NoteShare>.Failure(owned);

            var normalized = (userName ?? string.Empty).Trim().ToUpperInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);
            if (user is null)
                return Fail<NoteShare>(NoteErrors.NotFound, "No user has this username.", "userName");
            if (user.Id == userId)
                return Fail<NoteShare>(NoteErrors.Invalid, "The note is yours already.", "userName");

            var share = await _db.NoteShares.FirstOrDefaultAsync(s => s.NoteId == noteId && s.UserId == user.Id, ct);
            var isNew = share is null;
            if (share is null)
            {
                share = new NoteShare { NoteId = noteId, UserId = user.Id, CreatedAt = Now() };
                _db.NoteShares.Add(share);
            }
            share.CanEdit = canEdit;
            await _db.SaveChangesAsync(ct);
            share.User = user;

            if (isNew)
            {
                var note = await _db.Notes.AsNoTracking().Include(n => n.Owner).FirstAsync(n => n.Id == noteId, ct);
                await TellAsync(() => _events.SharedAsync(note, user.Id,
                    note.Owner?.UserName ?? note.Owner?.Name ?? string.Empty, canEdit, ct));
            }

            return ServiceResult<NoteShare>.Success(share);
        }

        /// <summary>Stops sharing the note with a user: its owner may, and so may the user themselves.</summary>
        public async Task<IServiceResult> UnshareAsync(Guid userId, Guid noteId, Guid sharedWith, CancellationToken ct = default)
        {
            if (userId != sharedWith)
            {
                var owned = await OwnedAsync(userId, noteId, ct);
                if (owned is not null)
                    return ServiceResult.Failure(owned);
            }

            var share = await _db.NoteShares.FirstOrDefaultAsync(s => s.NoteId == noteId && s.UserId == sharedWith, ct);
            if (share is null)
                return ServiceResult.Failure(new ServiceError(NoteErrors.NotFound, "The note is not shared with this user."));

            _db.NoteShares.Remove(share);
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        /// <summary>The user's own note with this title (ignoring case), the most recently changed one.</summary>
        public async Task<Note?> FindByTitleAsync(Guid ownerId, string title, CancellationToken ct = default)
        {
            var wanted = title.Trim().ToLower();
            return await _db.Notes.AsNoTracking()
                .Where(n => n.OwnerId == ownerId && n.Title.ToLower() == wanted)
                .OrderByDescending(n => n.UpdatedAt)
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>
        /// Sends the reminders that are due and clears them; returns how many.
        /// Each one is claimed first with a conditional update (only while it
        /// still holds the time it was found with), so with several servers
        /// running this only one sends it. A reminder that cannot be sent is
        /// put back for the next round.
        /// </summary>
        public async Task<int> SendDueRemindersAsync(int batch, CancellationToken ct = default)
        {
            var now = Now();
            var due = await _db.Notes
                .Where(n => n.RemindAt != null && n.RemindAt <= now)
                .OrderBy(n => n.RemindAt)
                .Take(batch)
                .ToListAsync(ct);

            var sent = 0;
            foreach (var note in due)
            {
                var at = note.RemindAt;
                note.RemindAt = null;
                if (!await TrySaveAsync(ct))
                    continue; // Another server took it, or the owner changed it meanwhile.

                try
                {
                    await _events.ReminderAsync(note, ct);
                    sent++;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogWarning(e, "Could not send the reminder of note {Id}", note.Id);
                    // Back for the next round, unless the owner set another one meanwhile.
                    note.RemindAt = at;
                    await TrySaveAsync(CancellationToken.None);
                }
            }
            return sent;
        }

        // False when a concurrency token no longer matched: the change is dropped.
        private async Task<bool> TrySaveAsync(CancellationToken ct)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateConcurrencyException e)
            {
                foreach (var entry in e.Entries)
                    entry.State = EntityState.Detached;
                return false;
            }
        }

        private async Task<NoteView?> FindAsync(Guid userId, Guid noteId, bool tracking, CancellationToken ct)
        {
            var notes = tracking ? _db.Notes : _db.Notes.AsNoTracking();
            var row = await notes
                .Where(n => n.Id == noteId && (n.OwnerId == userId || n.Shares.Any(s => s.UserId == userId)))
                .Select(n => new { Note = n, n.Owner, Share = n.Shares.FirstOrDefault(s => s.UserId == userId) })
                .FirstOrDefaultAsync(ct);
            if (row is null)
                return null;
            row.Note.Owner = row.Owner;
            return new NoteView(row.Note, AccessOf(row.Note, userId, row.Share));
        }

        // Null when the user owns the note; otherwise why they may not manage it.
        private async Task<ServiceError?> OwnedAsync(Guid userId, Guid noteId, CancellationToken ct)
        {
            var view = await FindAsync(userId, noteId, tracking: false, ct);
            if (view is null)
                return new ServiceError(NoteErrors.NotFound, "Note not found.");
            return view.Access == NoteAccess.Owner
                ? null
                : new ServiceError(NoteErrors.Forbidden, "Only the note's owner may manage who it is shared with.");
        }

        private static NoteAccess AccessOf(Note note, Guid userId, NoteShare? share) =>
            note.OwnerId == userId ? NoteAccess.Owner
            : share?.CanEdit == true ? NoteAccess.Edit
            : NoteAccess.Read;

        private const string TooLong = "The note is too long.";
        private const string ChangedMeanwhile = "The note changed meanwhile; open it again to see the change.";

        // Which input cannot be a note's and why, or null.
        private static (string Field, string Message)? Check(NoteInput input)
        {
            if ((input.Title ?? string.Empty).Trim().Length > NoteConfiguration.MaxTitleLength)
                return ("title", $"The title may be at most {NoteConfiguration.MaxTitleLength} characters.");
            if ((input.Body ?? string.Empty).Length > NoteConfiguration.MaxBodyLength)
                return ("body", TooLong);
            if (string.IsNullOrWhiteSpace(input.Title) && string.IsNullOrWhiteSpace(input.Body))
                return ("body", "A note needs a title or some text.");
            return null;
        }

        // The title given, or the text's first line without its Markdown heading marks.
        private static string TitleOf(NoteInput input)
        {
            var title = (input.Title ?? string.Empty).Trim();
            if (title.Length > 0)
                return title;
            var line = (input.Body ?? string.Empty).Split('\n')
                .Select(l => l.Trim().TrimStart('#', '>', '-', '*', ' ').Trim())
                .FirstOrDefault(l => l.Length > 0) ?? string.Empty;
            return line.Length <= DerivedTitleLength ? line : line[..(DerivedTitleLength - 1)].TrimEnd() + "…";
        }

        private async Task TellAsync(Func<Task> tell)
        {
            try
            {
                await tell();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Shared already: only the notification is lost.
                _logger.LogWarning(e, "Could not tell about a shared note");
            }
        }

        private DateTime Now() => _time.GetUtcNow().UtcDateTime;

        private static IServiceResult<T> Fail<T>(string code, string message, string? field = null) =>
            ServiceResult<T>.Failure(new ServiceError(code, message, field));

        private static IServiceResult<T> NotFound<T>() => Fail<T>(NoteErrors.NotFound, "Note not found.");
    }
}
