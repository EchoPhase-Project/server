// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Cql;
using EchoPhase.DAL.Scylla.Entities;

namespace EchoPhase.DAL.Scylla.Tests
{
    /// <summary>
    /// The application model must match the tables the migration creates, and
    /// run lookups must hit the owner's partition directly.
    /// </summary>
    public class ScyllaContextModelTests
    {
        private static ModelBuilder Model()
        {
            var builder = new ModelBuilder();
            ScyllaContext.Configure(builder);
            builder.Entity<BlockRun>().Validate();
            builder.Entity<BlockRunResult>().Validate();
            builder.Entity<ActiveBlockRun>().Validate();
            builder.Entity<Notification>().Validate();
            return builder;
        }

        [Fact]
        public void RunLookup_IsKeyedByOwnerAndRun()
        {
            var userId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            var query = QueryHarness.Query<BlockRun>().Where(r => r.UserId == userId && r.RunId == runId);

            var (cql, parameters) = QueryHarness.Translate(query, Model().Entity<BlockRun>());

            Assert.Contains("FROM block_runs WHERE user_id = ? AND run_id = ?", cql);
            Assert.DoesNotContain("ALLOW FILTERING", cql);
            Assert.Equal([userId, runId], parameters);
        }

        [Fact]
        public void History_IsOnePartitionPerUser()
        {
            var userId = Guid.NewGuid();

            var (cql, _) = QueryHarness.Translate(
                QueryHarness.Query<BlockRun>().Where(r => r.UserId == userId), Model().Entity<BlockRun>());

            Assert.Contains("FROM block_runs WHERE user_id = ?", cql);
        }

        [Fact]
        public void Insert_UsesSnakeCaseColumnsAndTtl()
        {
            var run = new BlockRun
            {
                UserId = Guid.NewGuid(),
                RunId = Guid.NewGuid(),
                Status = BlockRunStatus.Queued,
                BlockCount = 3,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var (cql, _) = new CqlGenerator().GenerateInsert(
                run, Model().Entity<BlockRun>(), new Models.WriteOptions(Ttl: TimeSpan.FromDays(1)));

            Assert.StartsWith("INSERT INTO block_runs (", cql);
            Assert.Contains("user_id", cql);
            Assert.Contains("block_count", cql);
            Assert.Contains("created_at", cql);
            Assert.EndsWith("USING TTL ?", cql);
        }

        [Fact]
        public void ActiveRuns_AreKeyedByRunAlone()
        {
            var keys = Model().Entity<ActiveBlockRun>().GetPrimaryKey();

            Assert.Equal([nameof(ActiveBlockRun.RunId)], keys);
        }

        [Fact]
        public void Inbox_IsOnePartitionPerUser_NewestFirstByTtl()
        {
            var userId = Guid.NewGuid();
            var (cql, _) = QueryHarness.Translate(
                QueryHarness.Query<Notification>().Where(n => n.UserId == userId), Model().Entity<Notification>());
            Assert.Contains("FROM notifications WHERE user_id = ?", cql);
            Assert.DoesNotContain("ALLOW FILTERING", cql);

            var (insert, _) = new CqlGenerator().GenerateInsert(
                new Notification { UserId = userId, Id = Guid.CreateVersion7(), Kind = "workflow", CreatedAt = DateTimeOffset.UtcNow },
                Model().Entity<Notification>(), new Models.WriteOptions(Ttl: TimeSpan.FromDays(30)));
            Assert.StartsWith("INSERT INTO notifications (", insert);
            Assert.Contains("created_at", insert);
            Assert.EndsWith("USING TTL ?", insert);
        }
    }
}
