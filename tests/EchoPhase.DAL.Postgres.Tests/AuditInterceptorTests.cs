// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using EchoPhase.DAL.Postgres.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace EchoPhase.DAL.Postgres.Tests
{
    /// <summary>
    /// The interceptor stamps entities from the injected clock, so the times
    /// are exact under a <see cref="FakeTimeProvider"/>.
    /// </summary>
    public class AuditInterceptorTests : IDisposable
    {
        private static readonly DateTimeOffset Start = new(2026, 3, 29, 0, 30, 0, TimeSpan.Zero);

        private readonly FakeTimeProvider _time = new(Start);
        private readonly AuditContext _context;

        public AuditInterceptorTests()
        {
            _context = new AuditContext(new DbContextOptionsBuilder<AuditContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(new AuditInterceptor(_time))
                .Options);
        }

        public void Dispose() => _context.Dispose();

        [Fact]
        public async Task Added_GetsCreatedAndUpdatedFromTheClock()
        {
            var note = new Note();
            _context.Notes.Add(note);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(Start.UtcDateTime, note.CreatedAt);
            Assert.Equal(Start.UtcDateTime, note.UpdatedAt);
            Assert.Equal(DateTimeKind.Utc, note.CreatedAt.Kind);
        }

        [Fact]
        public async Task Modified_MovesOnlyUpdatedAt()
        {
            var note = new Note();
            _context.Notes.Add(note);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _time.Advance(TimeSpan.FromHours(2));
            note.Text = "edited";
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(Start.UtcDateTime, note.CreatedAt);
            Assert.Equal(Start.UtcDateTime.AddHours(2), note.UpdatedAt);
        }

        [Fact]
        public async Task Deleted_IsSoftDeletedAtTheClockTime()
        {
            var note = new Note();
            _context.Notes.Add(note);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            _time.Advance(TimeSpan.FromDays(1));
            _context.Notes.Remove(note);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.True(note.IsDeleted);
            Assert.Equal(Start.UtcDateTime.AddDays(1), note.DeletedAt);
        }

        private sealed class Note : ITrackingEntity, ISoftDeletable
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            public string Text { get; set; } = "";
            public DateTime UpdatedAt { get; set; }
            public DateTime CreatedAt { get; set; }
            public bool IsDeleted { get; set; }
            public DateTime? DeletedAt { get; set; }
        }

        private sealed class AuditContext(DbContextOptions<AuditContext> options) : DbContext(options)
        {
            public DbSet<Note> Notes => Set<Note>();
        }
    }
}
