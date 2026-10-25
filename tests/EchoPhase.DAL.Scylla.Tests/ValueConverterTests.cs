// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.InMemory;
using EchoPhase.DAL.Scylla.Linq;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Tests
{
    public enum JobState
    {
        Queued,
        Running,
        Done,
    }

    public class Job
    {
        public Guid Owner { get; set; }
        public Guid Id { get; set; }
        public JobState State { get; set; }
        public JobState? Previous { get; set; }
        public Dictionary<string, int>? Meta { get; set; }
    }

    internal sealed class JobContext : DbContext
    {
        public DbSet<Job> Jobs { get; set; } = null!;

        public JobContext(ScyllaConnection connection) : base(connection)
        {
        }

        protected override void OnModelBuilding(ModelBuilder builder)
        {
            var job = builder.Entity<Job>();
            job.ToTable("jobs").HasPartitionKey<Guid>(j => j.Owner).HasClusteringKey<Guid>(j => j.Id);
            job.Property(j => j.State).HasConversion(ValueConverters.EnumToString<JobState>());
            job.Property(j => j.Previous).HasConversion(ValueConverters.EnumToNumber<JobState>());
            job.Property(j => j.Meta).HasConversion(ValueConverters.Json<Dictionary<string, int>>());
        }
    }

    /// <summary>HasConversion: stored as the column's type, read back as the property's, in queries and conditions too.</summary>
    public class ValueConverterTests : IDisposable
    {
        private readonly InMemoryCqlEngine _engine = new("echophase");
        private readonly ScyllaConnection _connection;
        private readonly Guid _owner = Guid.NewGuid();

        public ValueConverterTests()
        {
            _connection = new ScyllaConnection(_engine, "echophase");
            _engine.Execute(new CqlStatement(
                "CREATE TABLE jobs (owner uuid, id uuid, state text, previous int, meta text, PRIMARY KEY (owner, id))", []));
        }

        public void Dispose() => _connection.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task Values_RoundTrip_AndAreStoredAsTheColumnType()
        {
            using var db = new JobContext(_connection);
            var id = Guid.NewGuid();
            db.Jobs.Add(new Job { Owner = _owner, Id = id, State = JobState.Running, Previous = JobState.Queued, Meta = new() { ["tries"] = 2 } });
            await db.SaveChangesAsync(Ct);

            var raw = _engine.Execute(new CqlStatement("SELECT state, previous, meta FROM jobs WHERE owner = ?", [_owner])).Rows[0];
            Assert.Equal("Running", raw.GetValue<string>("state"));
            Assert.Equal(0, raw.GetValue<int>("previous"));
            Assert.Equal("{\"tries\":2}", raw.GetValue<string>("meta"));

            using var other = new JobContext(_connection);
            var job = await other.Jobs.Where(j => j.Owner == _owner).FirstAsync(Ct);
            Assert.Equal((JobState.Running, JobState.Queued, 2), (job.State, job.Previous, job.Meta!["tries"]));
        }

        [Fact]
        public async Task Queries_AndConditions_CompareConvertedValues()
        {
            using var db = new JobContext(_connection);
            var job = new Job { Owner = _owner, Id = Guid.NewGuid(), State = JobState.Queued };
            db.Jobs.Add(job);
            await db.SaveChangesAsync(Ct);

            var queued = await db.Jobs.Where(j => j.Owner == _owner && j.State == JobState.Queued).AllowFiltering().AsNoTracking().ToListAsync(Ct);
            Assert.Single(queued);

            var claim = new Job { Owner = _owner, Id = job.Id, State = JobState.Running };
            Assert.True(await db.Jobs.UpdateIfAsync(claim, j => j.State == JobState.Queued, ct: Ct));
            Assert.False(await db.Jobs.UpdateIfAsync(claim, j => j.State == JobState.Queued, ct: Ct));
        }

        [Fact]
        public void AConverterOfAnotherType_IsRefused()
        {
            var builder = new ModelBuilder();
            Assert.Throws<InvalidOperationException>(() =>
                builder.Entity<Job>().Property(j => j.Meta).HasConversion(ValueConverters.EnumToString<JobState>()));
        }
    }
}
