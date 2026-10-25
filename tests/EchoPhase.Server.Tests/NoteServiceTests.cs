// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace EchoPhase.Server.Tests
{
    /// <summary>NoteService's rules (titles, access, sharing, reminders) on EF's in-memory provider.</summary>
    public class NoteServiceTests : IDisposable
    {
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        private readonly string _dbName = Guid.NewGuid().ToString();
        private readonly InMemoryDatabaseRoot _root = new();
        private readonly PostgresContext _db;
        private readonly RecordingEvents _events = new();
        private readonly User _alice;
        private readonly User _bob;
        private readonly User _carol;

        public NoteServiceTests()
        {
            _db = new PostgresContext(new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(_dbName, _root)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            _alice = new User("Alice") { UserName = "alice", NormalizedUserName = "ALICE" };
            _bob = new User("Bob") { UserName = "bob", NormalizedUserName = "BOB" };
            _carol = new User("Carol") { UserName = "carol", NormalizedUserName = "CAROL" };
            _db.Users.AddRange(_alice, _bob, _carol);
            _db.SaveChanges();
        }

        public void Dispose() => _db.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private NoteService Notes() => new(_db, _events, _time, NullLogger<NoteService>.Instance);

        private async Task<Note> Create(User owner, string? title, string body = "text", bool pinned = false)
            => (await Notes().CreateAsync(owner.Id, new NoteInput(title, body, pinned), Ct)).Value!.Note;

        [Fact]
        public async Task Create_WithoutATitle_TakesTheFirstLine()
        {
            var note = await Create(_alice, "  ", "\n\n## Groceries\n- milk");

            Assert.Equal("Groceries", note.Title);
        }

        [Fact]
        public async Task Create_EmptyNote_IsRefused()
        {
            var result = await Notes().CreateAsync(_alice.Id, new NoteInput(" ", ""), Ct);

            Assert.Equal(NoteErrors.Invalid, result.Error.Code);
            Assert.Equal("body", result.Error.Field);
        }

        [Fact]
        public async Task TooLongTitle_NamesTheField()
        {
            var result = await Notes().CreateAsync(_alice.Id, new NoteInput(new string('t', 201), "x"), Ct);

            Assert.Equal("title", result.Error.Field);
        }

        [Fact]
        public async Task List_HasOwnAndSharedNotes_PinnedFirst()
        {
            var old = await Create(_alice, "old", pinned: true);
            _time.Advance(TimeSpan.FromMinutes(1));
            var recent = await Create(_alice, "recent");
            _time.Advance(TimeSpan.FromMinutes(1));
            var bobs = await Create(_bob, "bob's");
            await Create(_carol, "carol's");
            await Notes().ShareAsync(_bob.Id, bobs.Id, "alice", canEdit: false, Ct);

            var list = await Notes().ListAsync(_alice.Id, null, 100, Ct);

            // Pinned first, then the latest change first.
            Assert.Equal(["old", "bob's", "recent"], list.Select(v => v.Note.Title));
            Assert.Equal(3, list.Count);
            Assert.Equal(NoteAccess.Read, list.Single(v => v.Note.Id == bobs.Id).Access);
            Assert.Equal("bob", list.Single(v => v.Note.Id == bobs.Id).Note.Owner?.UserName);
            Assert.DoesNotContain(list, v => v.Note.Title == "carol's");
        }

        [Fact]
        public async Task List_SearchesTitleAndText_IgnoringCase()
        {
            await Create(_alice, "Shopping", "Milk and bread");
            await Create(_alice, "Ideas", "a bot that says hi");

            var found = await Notes().ListAsync(_alice.Id, "MILK", 100, Ct);

            Assert.Equal(["Shopping"], found.Select(v => v.Note.Title));
        }

        [Fact]
        public async Task OthersNote_IsNotFound()
        {
            var note = await Create(_alice, "secret");

            Assert.Equal(NoteErrors.NotFound, (await Notes().GetAsync(_bob.Id, note.Id, Ct)).Error.Code);
            Assert.Equal(NoteErrors.NotFound,
                (await Notes().UpdateAsync(_bob.Id, note.Id, new NoteInput("x", "y"), Ct)).Error.Code);
        }

        [Fact]
        public async Task SharedToRead_CannotBeChanged()
        {
            var note = await Create(_alice, "plan");
            await Notes().ShareAsync(_alice.Id, note.Id, "bob", canEdit: false, Ct);

            var result = await Notes().UpdateAsync(_bob.Id, note.Id, new NoteInput("mine", "now"), Ct);

            Assert.Equal(NoteErrors.Forbidden, result.Error.Code);
        }

        [Fact]
        public async Task SharedToEdit_ChangesTheTextButNotPinOrReminder()
        {
            var note = await Create(_alice, "plan", pinned: true);
            await Notes().ShareAsync(_alice.Id, note.Id, "BOB", canEdit: true, Ct);

            var result = await Notes().UpdateAsync(_bob.Id, note.Id,
                new NoteInput("plan v2", "more", Pinned: false, RemindAt: _time.GetUtcNow().AddDays(1)), Ct);

            Assert.True(result.Successful);
            var stored = await _db.Notes.AsNoTracking().SingleAsync(n => n.Id == note.Id, Ct);
            Assert.Equal("plan v2", stored.Title);
            Assert.True(stored.Pinned);
            Assert.Null(stored.RemindAt);
        }

        [Fact]
        public async Task OnlyTheOwner_DeletesAndShares()
        {
            var note = await Create(_alice, "plan");
            await Notes().ShareAsync(_alice.Id, note.Id, "bob", canEdit: true, Ct);

            Assert.Equal(NoteErrors.Forbidden, (await Notes().DeleteAsync(_bob.Id, note.Id, Ct)).Error.Code);
            Assert.Equal(NoteErrors.Forbidden, (await Notes().ShareAsync(_bob.Id, note.Id, "carol", false, Ct)).Error.Code);
            Assert.Equal(NoteErrors.Forbidden, (await Notes().SharesAsync(_bob.Id, note.Id, Ct)).Error.Code);
            Assert.True((await Notes().DeleteAsync(_alice.Id, note.Id, Ct)).Successful);
        }

        [Fact]
        public async Task Share_TellsTheUserOnce()
        {
            var note = await Create(_alice, "plan");

            await Notes().ShareAsync(_alice.Id, note.Id, "bob", canEdit: false, Ct);
            await Notes().ShareAsync(_alice.Id, note.Id, "bob", canEdit: true, Ct);

            var (shared, userId, by, canEdit) = Assert.Single(_events.Shared);
            Assert.Equal(note.Id, shared.Id);
            Assert.Equal(_bob.Id, userId);
            Assert.Equal("alice", by);
            Assert.False(canEdit);
            var share = Assert.Single((await Notes().SharesAsync(_alice.Id, note.Id, Ct)).Value!);
            Assert.True(share.CanEdit);
        }

        [Fact]
        public async Task Share_WithSelfOrNobody_IsRefused()
        {
            var note = await Create(_alice, "plan");

            Assert.Equal(NoteErrors.Invalid, (await Notes().ShareAsync(_alice.Id, note.Id, "alice", false, Ct)).Error.Code);
            var nobody = await Notes().ShareAsync(_alice.Id, note.Id, "nobody", false, Ct);
            Assert.Equal(NoteErrors.NotFound, nobody.Error.Code);
            Assert.Equal("userName", nobody.Error.Field);
        }

        [Fact]
        public async Task SharedUser_MayLeave()
        {
            var note = await Create(_alice, "plan");
            await Notes().ShareAsync(_alice.Id, note.Id, "bob", false, Ct);

            Assert.True((await Notes().UnshareAsync(_bob.Id, note.Id, _bob.Id, Ct)).Successful);
            Assert.Equal(NoteErrors.NotFound, (await Notes().GetAsync(_bob.Id, note.Id, Ct)).Error.Code);
        }

        [Fact]
        public async Task Append_AddsAParagraph()
        {
            var note = await Create(_alice, "log", "first");

            var result = await Notes().AppendAsync(_alice.Id, note.Id, "second", Ct);

            Assert.Equal("first\n\nsecond", result.Value!.Note.Body);
        }

        [Fact]
        public async Task DueReminders_AreSentOnceAndCleared()
        {
            var soon = (await Notes().CreateAsync(_alice.Id,
                new NoteInput("call", "x", RemindAt: _time.GetUtcNow().AddMinutes(5)), Ct)).Value!.Note;
            await Notes().CreateAsync(_alice.Id, new NoteInput("later", "x", RemindAt: _time.GetUtcNow().AddDays(1)), Ct);

            Assert.Equal(0, await Notes().SendDueRemindersAsync(100, Ct));
            _time.Advance(TimeSpan.FromMinutes(6));
            Assert.Equal(1, await Notes().SendDueRemindersAsync(100, Ct));
            Assert.Equal(0, await Notes().SendDueRemindersAsync(100, Ct));

            Assert.Equal([soon.Id], _events.Reminded.Select(n => n.Id));
            Assert.Null((await _db.Notes.AsNoTracking().SingleAsync(n => n.Id == soon.Id, Ct)).RemindAt);
        }

        [Fact]
        public async Task FailedReminder_StaysForTheNextRound()
        {
            await Notes().CreateAsync(_alice.Id, new NoteInput("call", "x", RemindAt: _time.GetUtcNow()), Ct);
            _events.FailReminders = true;

            Assert.Equal(0, await Notes().SendDueRemindersAsync(100, Ct));
            _events.FailReminders = false;
            Assert.Equal(1, await Notes().SendDueRemindersAsync(100, Ct));
        }

        [Fact]
        public async Task AReminderClaim_LosesToAnotherServersClaim()
        {
            await Notes().CreateAsync(_alice.Id, new NoteInput("call", "x", RemindAt: _time.GetUtcNow()), Ct);
            // A second server, its own context on the same database; both read it due.
            using var other = new PostgresContext(new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(_dbName, _root)
                .Options);
            var mine = await _db.Notes.SingleAsync(Ct);
            var theirs = await other.Notes.SingleAsync(Ct);

            theirs.RemindAt = null;
            await other.SaveChangesAsync(Ct);
            mine.RemindAt = null;

            // The save checks the stamp it read, which the other save renewed.
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => _db.SaveChangesAsync(Ct));
        }

        [Fact]
        public async Task Update_OverAnOlderStamp_IsAConflict()
        {
            var created = (await Notes().CreateAsync(_alice.Id, new NoteInput("plan", "v1"), Ct)).Value!.Note;
            var stamp = created.ConcurrencyStamp;
            var first = await Notes().UpdateAsync(_alice.Id, created.Id, new NoteInput("plan", "v2", ExpectedStamp: stamp), Ct);

            var second = await Notes().UpdateAsync(_alice.Id, created.Id, new NoteInput("plan", "v2b", ExpectedStamp: stamp), Ct);

            Assert.True(first.Successful);
            Assert.NotEqual(stamp, first.Value!.Note.ConcurrencyStamp);
            Assert.Equal(NoteErrors.Conflict, second.Error.Code);
        }

        private sealed class RecordingEvents : INoteEvents
        {
            public List<(Note Note, Guid UserId, string By, bool CanEdit)> Shared { get; } = [];
            public List<Note> Reminded { get; } = [];
            public bool FailReminders { get; set; }

            public Task SharedAsync(Note note, Guid userId, string sharedBy, bool canEdit, CancellationToken ct = default)
            {
                Shared.Add((note, userId, sharedBy, canEdit));
                return Task.CompletedTask;
            }

            public Task ReminderAsync(Note note, CancellationToken ct = default)
            {
                if (FailReminders)
                    throw new InvalidOperationException("down");
                Reminded.Add(note);
                return Task.CompletedTask;
            }
        }
    }
}
