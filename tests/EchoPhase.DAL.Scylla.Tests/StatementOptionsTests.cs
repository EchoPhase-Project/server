// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Cassandra;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.InMemory;
using EchoPhase.DAL.Scylla.Linq;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Tests
{
    /// <summary>Consistency and timeout per query and per write.</summary>
    public class StatementOptionsTests : IDisposable
    {
        private readonly InMemoryCqlEngine _engine = new("echophase");
        private readonly ScyllaConnection _connection;

        public StatementOptionsTests()
        {
            _connection = new ScyllaConnection(_engine, "echophase");
            _engine.Execute(new CqlStatement("CREATE TABLE profiles (id uuid PRIMARY KEY, name text, bio text, tags list<text>)", []));
        }

        public void Dispose() => _connection.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private CqlStatement Last(string start) => _engine.Statements.Last(s => s.Cql.StartsWith(start, StringComparison.Ordinal));

        [Fact]
        public async Task AQuery_CarriesItsConsistencyAndTimeout()
        {
            using var db = new ProfileContext(_connection);
            var id = Guid.NewGuid();

            await db.Profiles.Where(p => p.Id == id)
                .WithConsistency(ConsistencyLevel.LocalQuorum)
                .WithTimeout(TimeSpan.FromSeconds(2))
                .ToListAsync(Ct);

            var options = Must.Present(Last("SELECT").Options);
            Assert.Equal(ConsistencyLevel.LocalQuorum, options.Consistency);
            Assert.Equal(TimeSpan.FromSeconds(2), options.Timeout);
        }

        [Fact]
        public async Task AWrite_CarriesItsOptions_AndSerialIsRefusedForPlainWrites()
        {
            using var db = new ProfileContext(_connection);
            db.Profiles.Add(new Profile { Id = Guid.NewGuid(), Name = "a" },
                new WriteOptions(Statement: new StatementOptions(Consistency: ConsistencyLevel.All)));
            await db.SaveChangesAsync(Ct);
            Assert.Equal(ConsistencyLevel.All, Must.Present(Last("INSERT").Options).Consistency);

            db.Profiles.Add(new Profile { Id = Guid.NewGuid() },
                new WriteOptions(Statement: new StatementOptions(Consistency: ConsistencyLevel.Serial)));
            await Assert.ThrowsAsync<InvalidQueryException>(() => db.SaveChangesAsync(Ct));
        }
    }
}
