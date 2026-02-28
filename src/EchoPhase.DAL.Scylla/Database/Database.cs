using System.Reflection;
using Cassandra;
using EchoPhase.Configuration.Database.Scylla;
using EchoPhase.DAL.Scylla.Cql;
using EchoPhase.DAL.Scylla.Enums;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Loggers;
using EchoPhase.DAL.Scylla.Models;
using ISession = Cassandra.ISession;

namespace EchoPhase.DAL.Scylla
{
    public class Database : IDisposable
    {
        private readonly Dictionary<Guid, TrackedEntity> _changeTracker = new();
        private readonly Stack<List<TrackedEntity>> _transactionStack = new();
        private readonly IScyllaSettings _settings;
        private readonly Dictionary<Type, IEntityBuilder> _builderCache = new();
        private readonly List<IMigration> _migrations = new();
        private readonly Stack<Transaction> _activeTransactions = new();
        private readonly QueryExecutor _queryExecutor;
        private readonly IQueryLogger _queryLogger;
        private readonly ICqlGenerator _cqlGenerator;
        private ISaveStrategy _saveStrategy;

        public ISession Session
        {
            get;
        }
        public ModelBuilder ModelBuilder
        {
            get;
        }

        public Database(IScyllaSettings settings, IQueryLogger? logger = null)
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

                var migration = new MigrationBuilder();
                migration.CreateKeyspace(_settings.Keyspace, replicationFactor: 1);

                Session.Execute(migration.GetCommands().First());
                Session = cluster.Connect(_settings.Keyspace);
            }

            _cqlGenerator = new CqlGenerator();
            _queryExecutor = new QueryExecutor(Session, _queryLogger);
            _saveStrategy = new DirectSaveStrategy(_cqlGenerator, _queryExecutor, GetBuilder);
            _queryLogger = logger ?? new ConsoleQueryLogger();

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

        internal Transaction? CurrentTransaction =>
            _activeTransactions.Count > 0 ? _activeTransactions.Peek() : null;

        public void PushActiveTransaction(Transaction tx)
        {
            _activeTransactions.Push(tx);
            UpdateSaveStrategy();
        }

        public void PopActiveTransaction()
        {
            if (_activeTransactions.Count > 0)
                _activeTransactions.Pop();
            UpdateSaveStrategy();
        }

        private void UpdateSaveStrategy()
        {
            _saveStrategy = CurrentTransaction != null
                ? new TransactionSaveStrategy(_cqlGenerator, GetBuilder)
                : new DirectSaveStrategy(_cqlGenerator, _queryExecutor, GetBuilder);
        }

        public Transaction BeginTransaction(BatchType batchType = BatchType.Logged)
            => new Transaction(this, batchType);

        public int SaveChanges()
        {
            var changedEntities = GetChangedEntities();
            if (!changedEntities.Any()) return 0;

            var count = _saveStrategy.Execute(changedEntities, CurrentTransaction);

            MarkEntitiesAsUnchanged(changedEntities);
            _changeTracker.Clear();

            return count;
        }

        public async Task<int> SaveChangesAsync()
        {
            var changedEntities = GetChangedEntities();
            if (!changedEntities.Any()) return 0;

            var count = await _saveStrategy.ExecuteAsync(changedEntities, CurrentTransaction);

            MarkEntitiesAsUnchanged(changedEntities);
            _changeTracker.Clear();

            return count;
        }

        private List<TrackedEntity> GetChangedEntities() =>
            _changeTracker.Values
                .Where(t => t.State != EntityState.Unchanged)
                .ToList();

        private static void MarkEntitiesAsUnchanged(IEnumerable<TrackedEntity> entities)
        {
            foreach (var entity in entities)
                entity.State = EntityState.Unchanged;
        }

        #endregion

        #region Queries

        public IEnumerable<TResult> ExecuteQuery<TResult>(
            string cql,
            Func<Row, TResult>? projector = null,
            params object[] parameters)
        {
            return _queryExecutor.Execute(cql, projector, parameters);
        }

        public Task<IEnumerable<TResult>> ExecuteQueryAsync<TResult>(
            string cql,
            Func<Row, TResult>? projector = null,
            params object[] parameters)
        {
            return _queryExecutor.ExecuteAsync(cql, projector, parameters);
        }

        public IEnumerable<Row> ExecuteQuery(string cql, params object[] parameters)
            => _queryExecutor.Execute(cql, parameters);

        public Task<IEnumerable<Row>> ExecuteQueryAsync(string cql, params object[] parameters)
            => _queryExecutor.ExecuteAsync(cql, parameters);

        public TResult? ExecuteScalar<TResult>(string cql, params object[] parameters)
            => _queryExecutor.ExecuteScalar<TResult>(cql, parameters);

        public Task<TResult?> ExecuteScalarAsync<TResult>(string cql, params object[] parameters)
            => _queryExecutor.ExecuteScalarAsync<TResult>(cql, parameters);

        public void ExecuteNonQuery(string cql, params object[] parameters)
            => _queryExecutor.ExecuteNonQuery(cql, parameters);

        public Task ExecuteNonQueryAsync(string cql, params object[] parameters)
            => _queryExecutor.ExecuteNonQueryAsync(cql, parameters);

        #endregion

        #region Migrations

        private void EnsureMigrationsTable()
        {
            var migration = new MigrationBuilder();

            migration.CreateTable(table => table
                .WithKeyspace(_settings.Keyspace)
                .Text("migration_id", nullable: false)
                .Timestamp("applied_at")
                .PrimaryKey("migration_id"),
                ifNotExists: true
            );

            ExecuteQuery(migration.GetCommands().First());
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
            var applied = await ExecuteQueryAsync(
                "SELECT migration_id FROM schema_migrations",
                row => row.GetValue<string>("migration_id")
            );

            var appliedSet = applied.ToHashSet();
            return _migrations.Where(m => !appliedSet.Contains(m.Id));
        }

        public async Task MigrateAsync()
        {
            var pendingMigrations = (await GetPendingMigrationsAsync()).ToList();

            foreach (var migration in pendingMigrations)
            {
                try
                {
                    await migration.Up(this);

                    var valid = await migration.Validate(this);
                    if (!valid)
                        throw new InvalidOperationException($"Validation failed for migration {migration.Id}");

                    await ExecuteNonQueryAsync(
                        "INSERT INTO schema_migrations (migration_id, applied_at) VALUES (?, ?)",
                        migration.Id,
                        DateTime.UtcNow
                    );
                }
                catch (Exception)
                {
                    await migration.Down(this);
                    throw;
                }
            }
        }

        public void ApplyMigration(Action<MigrationBuilder> buildAction)
        {
            var migration = new MigrationBuilder();
            buildAction(migration);

            foreach (var command in migration.GetCommands())
            {
                ExecuteNonQuery(command);
            }
        }

        public async Task ApplyMigrationAsync(Action<MigrationBuilder> buildAction)
        {
            var migration = new MigrationBuilder();
            buildAction(migration);

            foreach (var command in migration.GetCommands())
            {
                await ExecuteNonQueryAsync(command);
            }
        }

        #endregion

        public void Dispose()
        {
            _changeTracker.Clear();
            _transactionStack.Clear();
            _queryExecutor.ClearPreparedStatements();
            Session?.Dispose();
        }
    }
}
