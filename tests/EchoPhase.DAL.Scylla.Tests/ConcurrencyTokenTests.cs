// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.InMemory;
using EchoPhase.DAL.Scylla.Linq;

namespace EchoPhase.DAL.Scylla.Tests
{
    public class Doc
    {
        public Guid Id { get; set; }
        public string? Text { get; set; }
        public string? Status { get; set; }
        public Guid Version { get; set; }
    }

    public class Counted
    {
        public Guid Id { get; set; }
        public string? Text { get; set; }
        public long Revision { get; set; }
    }

    internal sealed class DocContext : DbContext
    {
        public DbSet<Doc> Docs => Set<Doc>();
        public DbSet<Counted> CountedRows => Set<Counted>();

        public DocContext(ScyllaConnection connection) : base(connection)
        {
        }

        protected override void OnModelBuilding(ModelBuilder builder)
        {
            builder.Entity<Doc>().ToTable("docs").HasPartitionKey<Guid>(d => d.Id);
            builder.Entity<Doc>().Property(d => d.Version).IsConcurrencyToken();
            builder.Entity<Doc>().SetColumnName(nameof(Doc.Id), "id");
            builder.Entity<Doc>().SetColumnName(nameof(Doc.Text), "text");
            builder.Entity<Doc>().SetColumnName(nameof(Doc.Status), "status");
            builder.Entity<Doc>().SetColumnName(nameof(Doc.Version), "version");
            builder.Entity<Counted>().ToTable("counted").HasPartitionKey<Guid>(d => d.Id);
            builder.Entity<Counted>().Property(d => d.Revision).IsConcurrencyToken();
            builder.Entity<Counted>().SetColumnName(nameof(Counted.Id), "id");
            builder.Entity<Counted>().SetColumnName(nameof(Counted.Text), "text");
            builder.Entity<Counted>().SetColumnName(nameof(Counted.Revision), "revision");
        }
    }

    /// <summary>Concurrency tokens and explicit lightweight transactions, on the in-memory engine.</summary>
    public class ConcurrencyTokenTests : IDisposable
    {
        private readonly InMemoryCqlEngine _engine = new("echophase");
        private readonly ScyllaConnection _connection;

        public ConcurrencyTokenTests()
        {
            _connection = new ScyllaConnection(_engine, "echophase");
            _engine.Execute(new CqlStatement("CREATE TABLE docs (id uuid PRIMARY KEY, text text, status text, version uuid)", []));
            _engine.Execute(new CqlStatement("CREATE TABLE counted (id uuid PRIMARY KEY, text text, revision bigint)", []));
        }

        public void Dispose() => _connection.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private DocContext Context() => new(_connection);

        private async Task<Doc?> Find(Guid id)
        {
            using var db = Context();
            return await db.Docs.Where(d => d.Id == id).FirstOrDefaultAsync(Ct);
        }

        private async Task<Doc> Read(Guid id) => Must.Present(await Find(id));

        [Fact]
        public async Task Add_SetsAToken_AndASecondAddOfTheSameRowConflicts()
        {
            var id = Guid.NewGuid();
            using var db = Context();
            var doc = new Doc { Id = id, Text = "a" };
            db.Docs.Add(doc);
            await db.SaveChangesAsync(Ct);
            Assert.NotEqual(Guid.Empty, doc.Version);

            using var other = Context();
            other.Docs.Add(new Doc { Id = id, Text = "b" });
            var error = await Assert.ThrowsAsync<ScyllaConcurrencyException>(() => other.SaveChangesAsync(Ct));

            Assert.Single(error.Entities);
            Assert.Equal("a", (await Read(id)).Text);
        }

        [Fact]
        public async Task Update_OverAStaleToken_Conflicts_AndTheWinnerStays()
        {
            var id = Guid.NewGuid();
            using (var db = Context())
            {
                db.Docs.Add(new Doc { Id = id, Text = "v1" });
                await db.SaveChangesAsync(Ct);
            }
            var mine = await Read(id);
            var theirs = await Read(id);

            using var first = Context();
            theirs.Text = "theirs";
            first.Docs.Update(theirs);
            await first.SaveChangesAsync(Ct);
            Assert.NotEqual(mine.Version, theirs.Version);

            using var second = Context();
            mine.Text = "mine";
            second.Docs.Update(mine);
            await Assert.ThrowsAsync<ScyllaConcurrencyException>(() => second.SaveChangesAsync(Ct));

            Assert.Equal("theirs", (await Read(id)).Text);
            // Still tracked: detach to drop the lost change.
            second.Docs.Detach(mine);
            Assert.Equal(0, await second.SaveChangesAsync(Ct));
        }

        [Fact]
        public async Task Remove_OverAStaleToken_Conflicts()
        {
            var id = Guid.NewGuid();
            using (var db = Context())
            {
                db.Docs.Add(new Doc { Id = id, Text = "v1" });
                await db.SaveChangesAsync(Ct);
            }
            var stale = await Read(id);
            var fresh = await Read(id);
            using (var db = Context())
            {
                fresh.Text = "v2";
                db.Docs.Update(fresh);
                await db.SaveChangesAsync(Ct);
            }

            using var other = Context();
            other.Docs.Remove(stale);
            await Assert.ThrowsAsync<ScyllaConcurrencyException>(() => other.SaveChangesAsync(Ct));
            using var winner = Context();
            winner.Docs.Remove(fresh);
            await winner.SaveChangesAsync(Ct);
            Assert.Null(await Find(id));
        }

        [Fact]
        public async Task NumericTokens_CountUp()
        {
            using var db = Context();
            var row = new Counted { Id = Guid.NewGuid(), Text = "a" };
            db.CountedRows.Add(row);
            await db.SaveChangesAsync(Ct);
            row.Text = "b";
            db.CountedRows.Update(row);
            await db.SaveChangesAsync(Ct);

            Assert.Equal(2, row.Revision);
        }

        [Fact]
        public async Task Tokens_CannotGoInABatch()
        {
            using var db = Context();
            using var batch = db.Database.BeginBatch();
            db.Docs.Add(new Doc { Id = Guid.NewGuid() });

            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }

        [Fact]
        public async Task ExplicitConditions_ApplyOnlyWhenTheyHold()
        {
            var id = Guid.NewGuid();
            using var db = Context();
            var doc = new Doc { Id = id, Text = "job", Status = "queued" };
            Assert.True(await db.Docs.AddIfNotExistsAsync(doc, ct: Ct));
            Assert.False(await db.Docs.AddIfNotExistsAsync(new Doc { Id = id, Status = "other" }, ct: Ct));

            // Two workers claim the job; only the first finds it queued.
            var a = await Read(id);
            var b = await Read(id);
            a.Status = "running";
            b.Status = "running";
            var expected = "queued";
            Assert.True(await db.Docs.UpdateIfAsync(a, d => d.Status == expected, ct: Ct));
            Assert.False(await db.Docs.UpdateIfAsync(b, d => d.Status == "queued", ct: Ct));

            Assert.False(await db.Docs.RemoveIfAsync(new Doc { Id = Guid.NewGuid() }, ct: Ct));
            Assert.True(await db.Docs.RemoveIfAsync(await Read(id), ct: Ct));
            Assert.Null(await Find(id));
        }
    }
}
