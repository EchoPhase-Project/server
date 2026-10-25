// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.InMemory;
using EchoPhase.DAL.Scylla.Linq;

namespace EchoPhase.DAL.Scylla.Tests
{
    public class PageHits
    {
        public string Page { get; set; } = string.Empty;
        public long Views { get; set; }
    }

    public class Bag
    {
        public int Id { get; set; }
        public HashSet<string>? Tags { get; set; }
        public List<string>? Log { get; set; }
        public Dictionary<string, int>? Meta { get; set; }
        public string? Note { get; set; }
    }

    internal sealed class UpdateContext : DbContext
    {
        public DbSet<PageHits> Hits => Set<PageHits>();
        public DbSet<Bag> Bags => Set<Bag>();

        public UpdateContext(ScyllaConnection connection) : base(connection)
        {
        }

        protected override void OnModelBuilding(ModelBuilder builder)
        {
            builder.Entity<PageHits>().ToTable("hits").HasPartitionKey<string>(h => h.Page);
            builder.Entity<Bag>().ToTable("bags").HasPartitionKey<int>(b => b.Id);
        }
    }

    /// <summary>ExecuteUpdateAsync: counters and collections changed in place by Scylla.</summary>
    public class ExecuteUpdateTests : IDisposable
    {
        private readonly InMemoryCqlEngine _engine = new("echophase");
        private readonly ScyllaConnection _connection;

        public ExecuteUpdateTests()
        {
            _connection = new ScyllaConnection(_engine, "echophase");
            _engine.Execute(new CqlStatement("CREATE TABLE hits (page text PRIMARY KEY, views counter)", []));
            _engine.Execute(new CqlStatement(
                "CREATE TABLE bags (id int PRIMARY KEY, tags set<text>, log list<text>, meta map<text, int>, note text)", []));
            _engine.Execute(new CqlStatement("INSERT INTO bags (id, tags, log, meta, note) VALUES (1, {'a'}, ['x'], {'k': 1}, 'n')", []));
        }

        public void Dispose() => _connection.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task Counters_AddUp_WithoutReading()
        {
            using var db = new UpdateContext(_connection);
            await db.Hits.Where(h => h.Page == "/").ExecuteUpdateAsync(s => s.Increment(h => h.Views, 2), ct: Ct);
            await db.Hits.Where(h => h.Page == "/").ExecuteUpdateAsync(s => s.Increment(h => h.Views), ct: Ct);
            await db.Hits.Where(h => h.Page == "/").ExecuteUpdateAsync(s => s.Increment(h => h.Views, -1), ct: Ct);

            Assert.Equal(2, (await db.Hits.Where(h => h.Page == "/").AsNoTracking().FirstAsync(Ct)).Views);
        }

        [Fact]
        public async Task Collections_ChangeInPlace()
        {
            using var db = new UpdateContext(_connection);
            await db.Bags.Where(b => b.Id == 1).ExecuteUpdateAsync(s => s
                .Append(b => b.Tags, ["b", "a"])
                .Append(b => b.Log, ["y"])
                .Prepend(b => b.Log, ["w"])
                .Put(b => b.Meta, "j", 2)
                .Clear(b => b.Note), ct: Ct);
            await db.Bags.Where(b => b.Id == 1).ExecuteUpdateAsync(s => s
                .Remove(b => b.Tags, ["a"])
                .RemoveKeys(b => b.Meta, ["k"]), ct: Ct);

            var bag = await db.Bags.Where(b => b.Id == 1).AsNoTracking().FirstAsync(Ct);
            Assert.Equal(["b"], Must.Present(bag.Tags));
            Assert.Equal(["w", "x", "y"], bag.Log);
            Assert.Equal(new Dictionary<string, int> { ["j"] = 2 }, bag.Meta);
            Assert.Null(bag.Note);
        }

        [Fact]
        public async Task TheWholeKey_IsNeeded()
        {
            using var db = new UpdateContext(_connection);
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                db.Bags.ExecuteUpdateAsync(s => s.Set(b => b.Note, "x"), ct: Ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                db.Bags.Where(b => b.Id == 1).ExecuteUpdateAsync(s => s.Set(b => b.Id, 2), ct: Ct));
        }
    }
}
