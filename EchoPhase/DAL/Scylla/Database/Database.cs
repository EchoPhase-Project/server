using System.Diagnostics;
using System.Reflection;
using Cassandra;
using EchoPhase.DAL.Scylla.Cql;
using EchoPhase.DAL.Scylla.Enums;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Models;
using ISession = Cassandra.ISession;

namespace EchoPhase.DAL.Scylla
{
    public class Database : IDisposable
    {
        private readonly Dictionary<string, PreparedStatement> _preparedCache = new();
        private readonly Dictionary<Guid, TrackedEntity> _changeTracker = new();
        private readonly Stack<List<TrackedEntity>> _transactionStack = new();
        private readonly IScyllaSettings _settings;
        private readonly Dictionary<Type, IEntityBuilder> _builderCache = new();
        private readonly List<IMigration> _migrations = new();
        private readonly Stack<Transaction> _activeTransactions = new();

        public ISession Session
        {
            get;
        }
        public ModelBuilder ModelBuilder
        {
            get;
        }

        public Database(IScyllaSettings settings)
        {
            _settings = settings;
            ModelBuilder = new ModelBuilder();

            var clusterBuilder = Cluster.Builder()
                .AddContactPoint(_settings.ContactPoint);

            if (!string.IsNullOrWhiteSpace(_settings.Username))
                clusterBuilder = clusterBuilder.WithCredentials(_settings.Username, _settings.Password);

            var cluster = clusterBuilder.Build();

            try
            {
                Session = cluster.Connect(_settings.Keyspace);
            }
            catch (InvalidQueryException ex) when (ex.Message.Contains($"Keyspace '{_settings.Keyspace}' does not exist"))
            {
                Session = cluster.Connect();
                ExecuteQuery($@"
                    CREATE KEYSPACE IF NOT EXISTS {_settings.Keyspace}
                    WITH replication = {{'class': 'SimpleStrategy', 'replication_factor': 1}};
                ");

                Session = cluster.Connect(_settings.Keyspace);
            }

            EnsureMigrationsTable();
            LoadMigrationsFromAssembly(Assembly.GetExecutingAssembly());
        }

        #region Entity Operations

        public void Insert<T>(T entity) => TrackEntity(entity, EntityState.Added);
        public void Update<T>(T entity) => TrackEntity(entity, EntityState.Modified);
        public void Delete<T>(T entity) => TrackEntity(entity, EntityState.Deleted);

        private void TrackEntity<T>(T entity, EntityState state)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            TrackEntityInternal(entity, state, typeof(T));
        }

        private void TrackEntity(object entity, EntityState state)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            TrackEntityInternal(entity, state, entity.GetType());
        }

        private void TrackEntityInternal(object entity, EntityState state, Type type)
        {
            var tracked = _changeTracker.Values.FirstOrDefault(t => ReferenceEquals(t.Entity, entity));
            if (tracked == null)
            {
                tracked = new TrackedEntity(type, entity, state);
                _changeTracker[tracked.TrackingId] = tracked;
            }
            else
            {
                tracked.State = state;
            }
        }

        private IEntityBuilder GetBuilder(Type entityType)
        {
            if (!_builderCache.TryGetValue(entityType, out var builder))
            {
                var method = typeof(ModelBuilder)
                    .GetMethod(nameof(ModelBuilder.GetBuilder));

                if (method is null)
                    throw new InvalidOperationException($"Method {nameof(ModelBuilder.GetBuilder)} not found.");

                var generic = method.MakeGenericMethod(entityType);
                var obj = generic?.Invoke(ModelBuilder, null);
                if (obj is not IEntityBuilder entityBuilder)
                    throw new InvalidOperationException($"Method {generic?.Name} returned null or not IEntityBuilder");

                builder = entityBuilder;
                _builderCache[entityType] = builder;
            }
            return builder;
        }

        #endregion

        #region Transactions & SaveChanges

        public void PushActiveTransaction(Transaction tx) => _activeTransactions.Push(tx);
        public void PopActiveTransaction()
        {
            if (_activeTransactions.Any())
                _activeTransactions.Pop();
        }

        internal Transaction? CurrentTransaction => _activeTransactions.Any() ? _activeTransactions.Peek() : null;

        public Transaction BeginTransaction() => new Transaction(this);

        private (string cql, object[] parameters) GenerateInsertCql(object entity, IEntityBuilder builder)
        {
            var type = entity.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead).ToList();

            var columns = props.Select(p => builder.GetColumn(p.Name)).ToList();
            var placeholders = Enumerable.Repeat("?", columns.Count).ToList();
            var values = props.Select(p => p.GetValue(entity) ?? DBNull.Value).ToArray();

            var cql = $"INSERT INTO {builder.GetTableName()} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", placeholders)})";
            return (cql, values);
        }

        private (string cql, object[] parameters) GenerateUpdateCql(object entity, IEntityBuilder builder)
        {
            var pkNames = builder.GetPrimaryKey();
            if (pkNames == null || !pkNames.Any())
                throw new InvalidOperationException($"Primary key not defined for {entity.GetType().Name}");

            var type = entity.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanRead && !pkNames.Contains(p.Name))
                            .ToList();

            var setClauses = props.Select(p => $"{builder.GetColumn(p.Name)} = ?").ToList();
            var values = props.Select(p => p.GetValue(entity) ?? DBNull.Value).ToList();

            foreach (var pk in pkNames)
            {
                var pkProp = type.GetProperty(pk, BindingFlags.Public | BindingFlags.Instance)
                             ?? throw new InvalidOperationException($"Primary key property {pk} not found");
                values.Add(pkProp.GetValue(entity) ?? DBNull.Value);
            }

            var whereClause = string.Join(" AND ", pkNames.Select(pk => $"{builder.GetColumn(pk)} = ?"));

            var cql = $"UPDATE {builder.GetTableName()} SET {string.Join(", ", setClauses)} WHERE {whereClause}";
            return (cql, values.ToArray());
        }

        private (string cql, object[] parameters) GenerateDeleteCql(object entity, IEntityBuilder builder)
        {
            var pkNames = builder.GetPrimaryKey();
            if (pkNames == null || !pkNames.Any())
                throw new InvalidOperationException($"Primary key not defined for {entity.GetType().Name}");

            var type = entity.GetType();
            var values = new List<object>();

            foreach (var pk in pkNames)
            {
                var pkProp = type.GetProperty(pk, BindingFlags.Public | BindingFlags.Instance)
                             ?? throw new InvalidOperationException($"Primary key property {pk} not found");
                values.Add(pkProp.GetValue(entity) ?? DBNull.Value);
            }

            var whereClause = string.Join(" AND ", pkNames.Select(pk => $"{builder.GetColumn(pk)} = ?"));
            var cql = $"DELETE FROM {builder.GetTableName()} WHERE {whereClause}";
            return (cql, values.ToArray());
        }

        public int SaveChanges()
        {
            var actions = _changeTracker.Values.Where(t => t.State != EntityState.Unchanged).ToList();
            if (!actions.Any()) return 0;

            foreach (var tracked in actions)
            {
                var builder = GetBuilder(tracked.EntityType);

                (string? cql, object[] parameters) = tracked.State switch
                {
                    EntityState.Added => GenerateInsertCql(tracked.Entity, builder),
                    EntityState.Modified => GenerateUpdateCql(tracked.Entity, builder),
                    EntityState.Deleted => GenerateDeleteCql(tracked.Entity, builder),
                    _ => (null, Array.Empty<object>())
                };

                if (cql == null) continue;

                var bound = GetBoundStatement(cql, parameters);

                if (CurrentTransaction != null)
                    CurrentTransaction.AddToBatch(bound);
                else
                    Session.Execute(bound);
            }

            foreach (var tracked in actions)
                tracked.State = EntityState.Unchanged;

            _changeTracker.Clear();
            return 1;
        }

        public async Task<int> SaveChangesAsync()
        {
            var actions = _changeTracker.Values.Where(t => t.State != EntityState.Unchanged).ToList();
            if (!actions.Any()) return 0;

            foreach (var tracked in actions)
            {
                var builder = GetBuilder(tracked.EntityType);

                (string? cql, object[] parameters) = tracked.State switch
                {
                    EntityState.Added => GenerateInsertCql(tracked.Entity, builder),
                    EntityState.Modified => GenerateUpdateCql(tracked.Entity, builder),
                    EntityState.Deleted => GenerateDeleteCql(tracked.Entity, builder),
                    _ => (null, Array.Empty<object>())
                };

                if (cql == null) continue;

                var bound = await GetBoundStatementAsync(cql, parameters);

                if (CurrentTransaction != null)
                    CurrentTransaction.AddToBatch(bound);
                else
                    await Session.ExecuteAsync(bound);
            }

            foreach (var tracked in actions)
                tracked.State = EntityState.Unchanged;

            _changeTracker.Clear();
            return 1;
        }

        private BoundStatement GetBoundStatement(string cql, params object[] parameters)
        {
            if (!_preparedCache.TryGetValue(cql, out var prepared))
            {
                prepared = Session.Prepare(cql);
                _preparedCache[cql] = prepared;
            }
            return prepared.Bind(parameters);
        }

        private async Task<BoundStatement> GetBoundStatementAsync(string cql, params object[] parameters)
        {
            if (!_preparedCache.TryGetValue(cql, out var prepared))
            {
                prepared = await Session.PrepareAsync(cql);
                _preparedCache[cql] = prepared;
            }
            return prepared.Bind(parameters);
        }

        #endregion

        #region Queries

        public IEnumerable<TResult> ExecuteQuery<TResult>(string cql, Func<Row, TResult>? projector = null)
        {
            var stopwatch = Stopwatch.StartNew();

            LogQuery(cql, typeof(TResult), Array.Empty<object>());

            var rs = Session.Execute(cql);
            stopwatch.Stop();

            LogQueryTiming(stopwatch.ElapsedMilliseconds);

            if (projector != null) return rs.Select(projector);
            if (typeof(TResult) == typeof(Row)) return rs.Cast<TResult>();
            throw new InvalidOperationException("No projector provided for non-Row type");
        }

        public async Task<IEnumerable<TResult>> ExecuteQueryAsync<TResult>(
            string cql,
            Func<Row, TResult>? projector,
            params object[] parameters
        )
        {
            var stopwatch = Stopwatch.StartNew();

            LogQuery(cql, typeof(TResult), parameters);

            var stmt = await GetBoundStatementAsync(cql, parameters);
            var rs = await Session.ExecuteAsync(stmt);
            stopwatch.Stop();

            LogQueryTiming(stopwatch.ElapsedMilliseconds);

            if (projector != null) return rs.Select(projector).ToList();
            if (typeof(TResult) == typeof(Row)) return rs.Cast<TResult>().ToList();
            throw new InvalidOperationException("No projector provided for non-Row type");
        }

        public IEnumerable<Row> ExecuteQuery(string cql) => ExecuteQuery<Row>(cql, null);

        public Task<IEnumerable<Row>> ExecuteQueryAsync(string cql) => ExecuteQueryAsync<Row>(cql, null);

        private void LogQuery(string cql, Type resultType, object[] parameters)
        {
            var method = MethodBase.GetCurrentMethod();
            var ns = method?.DeclaringType?.Namespace;
            var className = method?.DeclaringType?.Name;
            var methodName = method?.Name;

            var fullName = $"{ns}.{className}.{methodName}";
            Console.WriteLine($"info: {fullName}[{DateTime.Now:HHmmss}]");
            Console.WriteLine($"      Executing query (returning {resultType.Name})");
            Console.WriteLine($"{CqlFormatter.FormatCql(cql)}");
        }

        private void LogQueryTiming(long elapsedMs)
        {
            Debug.WriteLine($"      Executed in {elapsedMs}ms\n");
        }

        #endregion

        #region Migrations

        private void EnsureMigrationsTable()
        {
            var cql = @"
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    migration_id text PRIMARY KEY,
                    applied_at timestamp
                );";
            ExecuteQuery(cql);
        }

        private void LoadMigrationsFromAssembly(Assembly assembly)
        {
            var migrationTypes = assembly.GetTypes()
                .Where(t => typeof(IMigration).IsAssignableFrom(t) && !t.IsAbstract);

            foreach (var type in migrationTypes)
            {
                var migration = (IMigration)Activator.CreateInstance(type)!;
                _migrations.Add(migration);
            }

            _migrations.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.Ordinal));
        }

        public async Task<IEnumerable<IMigration>> GetPendingMigrationsAsync()
        {
            var rs = await ExecuteQueryAsync("SELECT migration_id FROM schema_migrations;");
            var applied = rs.Select(r => r.GetValue<string>("migration_id")).ToHashSet();
            return _migrations.Where(m => !applied.Contains(m.Id));
        }

        public async Task MigrateAsync()
        {
            var pendingMigrations = (await GetPendingMigrationsAsync()).ToList();
            if (!pendingMigrations.Any())
            {
                return;
            }

            foreach (var migration in pendingMigrations)
            {
                try
                {
                    await migration.Up(this);
                    var valid = await migration.Validate(this);
                    if (!valid)
                        throw new InvalidOperationException($"Validation failed for migration {migration.Id}");

                    await ExecuteQueryAsync(
                        "INSERT INTO schema_migrations (migration_id, applied_at) VALUES (?, ?)",
                        row => row,
                        migration.Id, DateTime.UtcNow
                    );
                }
                catch
                {
                    try { await migration.Down(this); } catch { }
                    throw;
                }
            }
        }

        #endregion

        public void Dispose()
        {
            _changeTracker.Clear();
            _transactionStack.Clear();
            _preparedCache.Clear();
        }
    }
}
