// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using Cassandra;
using EchoPhase.Configuration.Database.Scylla;
using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Loggers;
using ISession = Cassandra.ISession;

namespace EchoPhase.DAL.Scylla.Database
{
    /// <summary>
    /// The expensive, thread-safe part of the ORM: one cluster connection,
    /// session and prepared-statement cache per application. Register it as a
    /// singleton; contexts built on it are cheap and can be scoped.
    /// </summary>
    /// <remarks>
    /// The driver is designed for a single long-lived <see cref="ICluster"/> and
    /// <see cref="ISession"/>; connecting per request costs a full handshake and
    /// leaks connections unless every one is disposed.
    /// </remarks>
    public sealed class ScyllaConnection : IDisposable
    {
        internal const string MigrationsTable = "schema_migrations";
        internal const string MigrationLockTable = "schema_migration_lock";

        private readonly ICluster _cluster;
        private readonly List<IMigration> _migrations;
        private bool _disposed;

        public ISession Session { get; }

        public string Keyspace { get; }

        internal QueryExecutor Executor { get; }

        internal IQueryLogger Logger { get; }

        internal IReadOnlyList<IMigration> Migrations => _migrations;

        /// <param name="migrationAssemblies">
        /// Where to look for <see cref="IMigration"/> types; defaults to this ORM's assembly.
        /// </param>
        public ScyllaConnection(
            IScyllaOptions settings,
            IQueryLogger? logger = null,
            IEnumerable<Assembly>? migrationAssemblies = null)
        {
            Keyspace = settings.Keyspace;
            Logger = logger ?? NullQueryLogger.Instance;

            _cluster = BuildCluster(settings);

            try
            {
                Session = Connect(settings);
            }
            catch
            {
                _cluster.Dispose();
                throw;
            }

            Executor = new QueryExecutor(Session, Logger);

            // Once per application rather than once per context.
            EnsureMigrationTables();
            _migrations = LoadMigrations(migrationAssemblies ?? [Assembly.GetExecutingAssembly()]);
        }

        private static ICluster BuildCluster(IScyllaOptions settings)
        {
            var contactPoints = settings.ContactPoint
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var builder = Cluster.Builder()
                .AddContactPoints(contactPoints)
                .WithPort(settings.Port)
                // Token-aware routing on top of DC-aware round robin: each request
                // goes straight to a replica of its partition in the local DC.
                .WithLoadBalancingPolicy(string.IsNullOrWhiteSpace(settings.LocalDatacenter)
                    ? Policies.DefaultLoadBalancingPolicy
                    : Policies.NewDefaultLoadBalancingPolicy(settings.LocalDatacenter))
                .WithQueryOptions(new QueryOptions()
                    .SetConsistencyLevel(Enum.Parse<ConsistencyLevel>(settings.Consistency, ignoreCase: true))
                    .SetSerialConsistencyLevel(Enum.Parse<ConsistencyLevel>(settings.SerialConsistency, ignoreCase: true)))
                .WithSocketOptions(new SocketOptions()
                    .SetReadTimeoutMillis((int)settings.RequestTimeout.TotalMilliseconds));

            if (!string.IsNullOrWhiteSpace(settings.Username))
                builder = builder.WithCredentials(settings.Username, settings.Password);

            return builder.Build();
        }

        private ISession Connect(IScyllaOptions settings)
        {
            try
            {
                return _cluster.Connect(settings.Keyspace);
            }
            catch (InvalidQueryException ex) when (ex.Message.Contains($"Keyspace '{settings.Keyspace}' does not exist"))
            {
                // A keyspace silently created with RF=1 would
                // lose data on a real cluster; it must be asked for.
                if (!settings.AutoCreateKeyspace)
                    throw new InvalidOperationException(
                        $"Keyspace '{settings.Keyspace}' does not exist. Create it (NetworkTopologyStrategy " +
                        "for clusters) or set Database:Scylla:AutoCreateKeyspace=true for single-node development.", ex);

                using (var bootstrap = _cluster.Connect())
                {
                    var migration = new MigrationBuilder();
                    migration.CreateKeyspace(settings.Keyspace, replicationFactor: settings.ReplicationFactor);
                    bootstrap.Execute(migration.GetCommands().First());
                }

                return _cluster.Connect(settings.Keyspace);
            }
        }

        private void EnsureMigrationTables()
        {
            var migration = new MigrationBuilder(Keyspace);

            migration.CreateTable(MigrationsTable, table => table
                    .Text("migration_id")
                    .Timestamp("applied_at")
                    .PrimaryKey("migration_id"),
                ifNotExists: true);

            // One row, taken with a lightweight transaction, so concurrently
            // starting instances do not apply the same migration twice.
            migration.CreateTable(MigrationLockTable, table => table
                    .Text("lock_id")
                    .Uuid("owner")
                    .Timestamp("acquired_at")
                    .PrimaryKey("lock_id"),
                ifNotExists: true);

            foreach (var command in migration.GetCommands())
                Executor.ExecuteNonQuery(command);
        }

        private static List<IMigration> LoadMigrations(IEnumerable<Assembly> assemblies)
        {
            var migrations = assemblies
                .Distinct()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IMigration).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                .Select(t => (IMigration)Activator.CreateInstance(t)!)
                .ToList();

            var duplicate = migrations.GroupBy(m => m.Id).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
                throw new InvalidOperationException($"Duplicate Scylla migration id '{duplicate.Key}'.");

            migrations.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.Ordinal));
            return migrations;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Executor.ClearPreparedStatements();
            Session.Dispose();
            _cluster.Dispose();
        }
    }
}
