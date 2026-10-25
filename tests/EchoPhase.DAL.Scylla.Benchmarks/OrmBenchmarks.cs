// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using BenchmarkDotNet.Attributes;
using Cassandra;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.DAL.Scylla.InMemory;
using EchoPhase.DAL.Scylla.Linq;

namespace EchoPhase.DAL.Scylla.Benchmarks
{
    /// <summary>
    /// Answers every SELECT with the same rows, writes nothing: what remains is
    /// the ORM's own work.
    /// </summary>
    internal sealed class CannedEngine : ICqlEngine
    {
        public IReadOnlyList<Row> Rows { get; set; } = [];

        public CqlResult Execute(CqlStatement statement)
            => statement.Cql.StartsWith("SELECT", StringComparison.Ordinal) ? new CqlResult(Rows, null) : CqlResult.Empty;

        public Task<CqlResult> ExecuteAsync(CqlStatement statement, CancellationToken ct = default)
            => Task.FromResult(Execute(statement));

        public void ExecuteBatch(IReadOnlyList<CqlStatement> statements, BatchType type)
        {
        }

        public Task ExecuteBatchAsync(IReadOnlyList<CqlStatement> statements, BatchType type, CancellationToken ct = default)
            => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    [MemoryDiagnoser]
    public class OrmBenchmarks
    {
        private readonly Guid _user = Guid.NewGuid();
        private readonly CannedEngine _engine = new();
        private ScyllaConnection? _connection;
        private IReadOnlyList<Row> _hundred = [];
        private IReadOnlyList<Row> _one = [];

        [GlobalSetup]
        public void Setup()
        {
            // Real rows, as the driver hands them: made by the in-memory engine.
            var memory = new InMemoryCqlEngine("bench");
            using (var seed = new ScyllaConnection(memory, "bench"))
            using (var db = new ScyllaContext(seed))
            {
                db.Database.MigrateAsync().GetAwaiter().GetResult();
                for (var i = 0; i < 100; i++)
                    db.BlockRuns.Add(new BlockRun
                    {
                        UserId = _user,
                        RunId = Guid.CreateVersion7(),
                        Status = "succeeded",
                        Trigger = "manual",
                        BlockCount = 5,
                        CreatedAt = DateTimeOffset.UtcNow,
                        StartedAt = DateTimeOffset.UtcNow,
                        FinishedAt = DateTimeOffset.UtcNow,
                        DurationMs = 12,
                        Steps = 9,
                        ErrorCount = 0,
                    });
                db.SaveChanges();
            }
            _hundred = memory.Execute(new CqlStatement("SELECT * FROM block_runs", [])).Rows;
            _one = [_hundred[0]];

            _connection = new ScyllaConnection(_engine, "bench");
        }

        [GlobalCleanup]
        public void Cleanup() => _connection?.Dispose();

        private ScyllaConnection Connection => _connection ?? throw new InvalidOperationException("Setup has not run.");

        [Benchmark(Description = "translate only (no rows)")]
        public async Task<int> Translate()
        {
            _engine.Rows = [];
            using var db = new ScyllaContext(Connection);
            return (await db.BlockRuns.Where(r => r.UserId == _user).AsNoTracking().ToListAsync()).Count;
        }

        [Benchmark(Description = "first by key, tracked")]
        public async Task<BlockRun?> FirstByKey()
        {
            _engine.Rows = _one;
            using var db = new ScyllaContext(Connection);
            var id = Guid.Empty;
            return await db.BlockRuns.Where(r => r.UserId == _user && r.RunId == id).FirstOrDefaultAsync();
        }

        [Benchmark(Description = "100 rows, no tracking")]
        public async Task<int> HundredUntracked()
        {
            _engine.Rows = _hundred;
            using var db = new ScyllaContext(Connection);
            return (await db.BlockRuns.Where(r => r.UserId == _user).AsNoTracking().ToListAsync()).Count;
        }

        [Benchmark(Description = "100 rows, tracked")]
        public async Task<int> HundredTracked()
        {
            _engine.Rows = _hundred;
            using var db = new ScyllaContext(Connection);
            return (await db.BlockRuns.Where(r => r.UserId == _user).ToListAsync()).Count;
        }

        [Benchmark(Description = "insert one")]
        public Task<int> Insert()
        {
            using var db = new ScyllaContext(Connection);
            db.BlockRuns.Add(new BlockRun { UserId = _user, RunId = Guid.NewGuid(), Status = "queued", BlockCount = 3 });
            return db.SaveChangesAsync();
        }

        [Benchmark(Description = "100 tracked, change one, save")]
        public async Task<int> DetectChanges()
        {
            _engine.Rows = _hundred;
            using var db = new ScyllaContext(Connection);
            var runs = await db.BlockRuns.Where(r => r.UserId == _user).ToListAsync();
            runs[0].Status = "failed";
            return await db.SaveChangesAsync();
        }
    }
}
