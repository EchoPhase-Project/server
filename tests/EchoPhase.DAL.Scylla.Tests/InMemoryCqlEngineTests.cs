// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Cassandra;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.DAL.Scylla.InMemory;
using EchoPhase.DAL.Scylla.Linq;
using EchoPhase.DAL.Scylla.Models;
using Microsoft.Extensions.Time.Testing;

namespace EchoPhase.DAL.Scylla.Tests
{
    /// <summary>
    /// The in-memory engine: the application's migrations and model on it,
    /// and Scylla's rules (what needs ALLOW FILTERING, TTLs, timestamps, LWT).
    /// </summary>
    public class InMemoryCqlEngineTests : IDisposable
    {
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        private readonly InMemoryCqlEngine _engine;
        private readonly ScyllaConnection _connection;

        public InMemoryCqlEngineTests()
        {
            _engine = new InMemoryCqlEngine("echophase", _time);
            _connection = new ScyllaConnection(_engine, "echophase");
        }

        public void Dispose() => _connection.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<ScyllaContext> Migrated()
        {
            var db = new ScyllaContext(_connection);
            db.Database.TimeProvider = _time;
            await db.Database.MigrateAsync(Ct);
            return db;
        }

        private CqlResult Run(string cql, params object?[] values) => _engine.Execute(new CqlStatement(cql, values));

        [Fact]
        public async Task Migrations_Apply_AndTheModelMatchesThem()
        {
            using var db = await Migrated();

            Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
            Assert.Empty(await db.ValidateSchemaAsync(Ct));
            // Applying again is a no-op.
            await db.Database.MigrateAsync(Ct);
        }

        [Fact]
        public async Task Runs_AreReadNewestFirst_FromTheOwnersPartition()
        {
            using var db = await Migrated();
            var user = Guid.NewGuid();
            var ids = new List<Guid>();
            for (var i = 0; i < 3; i++)
            {
                var id = Guid.CreateVersion7(_time.GetUtcNow());
                ids.Add(id);
                db.BlockRuns.Add(new BlockRun { UserId = user, RunId = id, Status = "queued", CreatedAt = _time.GetUtcNow() });
                _time.Advance(TimeSpan.FromSeconds(1));
            }
            db.BlockRuns.Add(new BlockRun { UserId = Guid.NewGuid(), RunId = Guid.NewGuid(), Status = "queued" });
            await db.SaveChangesAsync(Ct);

            var runs = await db.BlockRuns.Where(r => r.UserId == user).ToListAsync(Ct);
            var page = await db.BlockRuns.Where(r => r.UserId == user).ToPageAsync(2, null, Ct);
            var rest = await db.BlockRuns.Where(r => r.UserId == user).ToPageAsync(2, page.NextPagingState, Ct);

            Assert.Equal(ids.AsEnumerable().Reverse(), runs.Select(r => r.RunId));
            Assert.Equal(2, page.Items.Count);
            Assert.Single(rest.Items);
            Assert.False(rest.HasMore);
            Assert.Equal(3, await db.BlockRuns.Where(r => r.UserId == user).CountAsync(Ct));
        }

        [Fact]
        public async Task UpdateAndRemove_WriteThrough()
        {
            using var db = await Migrated();
            var run = new BlockRun { UserId = Guid.NewGuid(), RunId = Guid.NewGuid(), Status = "queued", BlockCount = 2 };
            db.BlockRuns.Add(run);
            await db.SaveChangesAsync(Ct);

            run.Status = "succeeded";
            db.BlockRuns.Update(run);
            await db.SaveChangesAsync(Ct);
            var read = await db.BlockRuns.Where(r => r.UserId == run.UserId && r.RunId == run.RunId).FirstOrDefaultAsync(Ct);
            Assert.Equal("succeeded", read!.Status);
            Assert.Equal(2, read.BlockCount);

            db.BlockRuns.Remove(run);
            await db.SaveChangesAsync(Ct);
            Assert.Null(await db.BlockRuns.Where(r => r.UserId == run.UserId && r.RunId == run.RunId).FirstOrDefaultAsync(Ct));
        }

        [Fact]
        public async Task Ttl_ExpiresRowsOnTheClock()
        {
            using var db = await Migrated();
            var user = Guid.NewGuid();
            db.Notifications.Add(new Notification { UserId = user, Id = Guid.NewGuid(), Kind = "workflow", CreatedAt = _time.GetUtcNow() },
                new WriteOptions(Ttl: TimeSpan.FromMinutes(5)));
            await db.SaveChangesAsync(Ct);

            Assert.Single(await db.Notifications.Where(n => n.UserId == user).ToListAsync(Ct));
            _time.Advance(TimeSpan.FromMinutes(6));
            Assert.Empty(await db.Notifications.Where(n => n.UserId == user).ToListAsync(Ct));
        }

        [Fact]
        public async Task Timestamps_LastWriteWins()
        {
            await Migrated();
            var id = Guid.NewGuid();
            Run("INSERT INTO block_runs_active (run_id, user_id) VALUES (?, ?) USING TIMESTAMP 2000", id, Guid.Empty);
            var newer = Guid.NewGuid();
            Run("UPDATE block_runs_active USING TIMESTAMP 3000 SET user_id = ? WHERE run_id = ?", newer, id);
            Run("UPDATE block_runs_active USING TIMESTAMP 1000 SET user_id = ? WHERE run_id = ?", Guid.NewGuid(), id);

            var row = Run("SELECT user_id FROM block_runs_active WHERE run_id = ?", id).Rows.Single();
            Assert.Equal(newer, row.GetValue<Guid>("user_id"));

            Run("DELETE FROM block_runs_active USING TIMESTAMP 2500 WHERE run_id = ?", id);
            // The delete is older than the update: the updated cell survives.
            Assert.Single(Run("SELECT user_id FROM block_runs_active WHERE run_id = ?", id).Rows);
        }

        [Fact]
        public async Task Filtering_OnARegularColumn_NeedsAllowFiltering()
        {
            await Migrated();
            var error = Assert.Throws<InvalidQueryException>(() =>
                Run("SELECT * FROM block_runs WHERE user_id = ? AND status = ?", Guid.NewGuid(), "queued"));
            Assert.Contains("ALLOW FILTERING", error.Message);

            Run("SELECT * FROM block_runs WHERE user_id = ? AND status = ? ALLOW FILTERING", Guid.NewGuid(), "queued");
            Run("SELECT * FROM block_runs");
            Assert.Throws<InvalidQueryException>(() => Run("SELECT * FROM block_runs WHERE status = ?", "queued"));
            Assert.Throws<InvalidQueryException>(() => Run("SELECT * FROM block_runs WHERE user_id = ? AND status != ?", Guid.NewGuid(), "x"));
        }

        [Fact]
        public async Task Order_FollowsTheClusteringKey()
        {
            await Migrated();
            var user = Guid.NewGuid();
            Run("SELECT * FROM block_runs WHERE user_id = ? ORDER BY run_id ASC", user);
            Assert.Throws<InvalidQueryException>(() => Run("SELECT * FROM block_runs WHERE user_id = ? ORDER BY status", user));
            Assert.Throws<InvalidQueryException>(() => Run("SELECT * FROM block_runs ORDER BY run_id"));
        }

        [Fact]
        public async Task Writes_NeedTheWholeKey()
        {
            await Migrated();
            Assert.Throws<InvalidQueryException>(() => Run("INSERT INTO block_runs (user_id, status) VALUES (?, ?)", Guid.NewGuid(), "x"));
            Assert.Throws<InvalidQueryException>(() => Run("UPDATE block_runs SET status = ? WHERE user_id = ?", "x", Guid.NewGuid()));
            Assert.Throws<InvalidQueryException>(() => Run("INSERT INTO block_runs (user_id, run_id, nope) VALUES (?, ?, ?)", Guid.NewGuid(), Guid.NewGuid(), 1));
            Assert.Throws<InvalidQueryException>(() => Run("INSERT INTO block_runs (user_id, run_id, steps) VALUES (?, ?, ?)", Guid.NewGuid(), Guid.NewGuid(), "many"));
        }

        [Fact]
        public async Task LightweightTransactions_ApplyOnce()
        {
            await Migrated();
            var id = Guid.NewGuid();
            var first = Run("INSERT INTO block_runs_active (run_id, user_id) VALUES (?, ?) IF NOT EXISTS", id, Guid.Empty);
            var second = Run("INSERT INTO block_runs_active (run_id, user_id) VALUES (?, ?) IF NOT EXISTS", id, Guid.NewGuid());

            Assert.True(first.Applied);
            Assert.False(second.Applied);
            Assert.Equal(Guid.Empty, second.Rows[0].GetValue<Guid>("user_id"));

            var owner = Guid.NewGuid();
            Assert.False(Run("UPDATE block_runs_active SET user_id = ? WHERE run_id = ? IF user_id = ?", owner, id, Guid.NewGuid()).Applied);
            Assert.True(Run("UPDATE block_runs_active SET user_id = ? WHERE run_id = ? IF user_id = ?", owner, id, Guid.Empty).Applied);
            Assert.True(Run("DELETE FROM block_runs_active WHERE run_id = ? IF EXISTS", id).Applied);
            Assert.False(Run("DELETE FROM block_runs_active WHERE run_id = ? IF EXISTS", id).Applied);
        }

        [Fact]
        public void CountersAndCollections()
        {
            Run("CREATE TABLE hits (page text PRIMARY KEY, views counter)");
            Run("UPDATE hits SET views = views + ? WHERE page = ?", 2L, "/");
            Run("UPDATE hits SET views = views + 3 WHERE page = '/'");
            Assert.Equal(5L, Run("SELECT views FROM hits WHERE page = ?", "/").Rows[0].GetValue<long>("views"));
            Assert.Throws<InvalidQueryException>(() => Run("UPDATE hits SET views = 1 WHERE page = '/'"));

            Run("CREATE TABLE bags (id int PRIMARY KEY, tags set<text>, log list<text>, meta map<text, int>)");
            Run("INSERT INTO bags (id, tags, log, meta) VALUES (1, {'b', 'a'}, ['x'], {'k': 1})");
            Run("UPDATE bags SET tags = tags + ?, log = log + ?, meta['j'] = 2 WHERE id = 1", new[] { "a", "c" }, new[] { "y" });
            var row = Run("SELECT * FROM bags WHERE id = 1").Rows[0];
            Assert.Equal(["a", "b", "c"], row.GetValue<string[]>("tags"));
            Assert.Equal(["x", "y"], row.GetValue<string[]>("log"));
            Assert.Equal(2, row.GetValue<IDictionary<string, int>>("meta")["j"]);
            Assert.Single(Run("SELECT * FROM bags WHERE tags CONTAINS 'c' ALLOW FILTERING").Rows);
        }

        [Fact]
        public void SystemSchema_DescribesTables()
        {
            Run("CREATE TABLE t (a int, b text, c bigint, PRIMARY KEY ((a), b)) WITH CLUSTERING ORDER BY (b DESC)");
            var columns = Run("SELECT column_name, kind, position, type FROM system_schema.columns WHERE keyspace_name = ? AND table_name = ?",
                "echophase", "t").Rows.ToDictionary(r => r.GetValue<string>("column_name"));

            Assert.Equal("partition_key", columns["a"].GetValue<string>("kind"));
            Assert.Equal("clustering", columns["b"].GetValue<string>("kind"));
            Assert.Equal("regular", columns["c"].GetValue<string>("kind"));
            Assert.Equal("bigint", columns["c"].GetValue<string>("type"));
            Assert.Single(Run("SELECT table_name FROM system_schema.tables WHERE keyspace_name = ? AND table_name = ?", "echophase", "t").Rows);
        }

        [Fact]
        public void BindMarkers_MustMatchTheValues()
        {
            Run("CREATE TABLE t (a int PRIMARY KEY, b text)");
            Assert.Throws<InvalidQueryException>(() => Run("INSERT INTO t (a, b) VALUES (?, ?)", 1));
            Assert.Throws<InvalidQueryException>(() => Run("INSERT INTO t (a) VALUES (?)", 1, "extra"));
        }
    }
}
