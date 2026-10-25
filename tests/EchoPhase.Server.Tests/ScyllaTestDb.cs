// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.InMemory;

namespace EchoPhase.Server.Tests
{
    /// <summary>ScyllaDB in memory with the application's migrations applied, on a given clock.</summary>
    public sealed class ScyllaTestDb : IDisposable
    {
        private readonly ScyllaConnection _connection;

        public ScyllaTestDb(TimeProvider time)
        {
            Engine = new InMemoryCqlEngine("echophase", time);
            _connection = new ScyllaConnection(Engine, "echophase");
            using var db = Context();
            db.Database.TimeProvider = time;
            db.Database.MigrateAsync().GetAwaiter().GetResult();
        }

        public InMemoryCqlEngine Engine { get; }

        /// <summary>A new context on the shared data, as each request gets one.</summary>
        public ScyllaContext Context() => new(_connection);

        public void Dispose() => _connection.Dispose();
    }
}
