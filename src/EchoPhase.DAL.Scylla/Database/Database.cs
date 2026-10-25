// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using Cassandra;
using EchoPhase.Configuration.Database.Scylla;
using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Cql;
using EchoPhase.DAL.Scylla.Enums;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Models;
using EchoPhase.DAL.Scylla.Schema;

namespace EchoPhase.DAL.Scylla.Database
{
    /// <summary>
    /// Unit of work for one context: change tracking, batches and the model.
    /// The connection underneath is shared (<see cref="ScyllaConnection"/>).
    /// </summary>
    /// <remarks>
    /// Meant for one logical operation at a time, like an EF DbContext. Tracking
    /// and batch state are guarded, so concurrent calls do not corrupt it, but
    /// use one context per unit of work rather than sharing one.
    /// </remarks>
    public class Database : IDisposable
    {
        private const string MigrationLockId = "migrations";
        private static readonly TimeSpan MigrationLockTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan MigrationLockWait = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan MigrationLockPoll = TimeSpan.FromSeconds(2);

        private readonly object _sync = new();
        private readonly Dictionary<object, TrackedEntity> _changeTracker = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<(Type, string), object> _identities = new();
        private readonly Dictionary<Type, IEntityBuilder> _builderCache = new();
        private readonly Stack<Batch> _activeBatches = new();

        private readonly ScyllaConnection _connection;
        private readonly bool _ownsConnection;
        private readonly ISaveStrategy _directStrategy;
        private readonly ISaveStrategy _batchStrategy;
        private bool _disposed;

        public string Keyspace => _connection.Keyspace;

        public ModelBuilder ModelBuilder
        {
            get;
        }

        internal QueryExecutor Executor => _connection.Executor;

        /// <summary>
        /// Clock for migration bookkeeping (applied time, lock deadline);
        /// tests may replace it.
        /// </summary>
        public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

        /// <summary>
        /// Works on a shared connection; disposing this does not close it.
        /// </summary>
        /// <param name="model">A model built once and shared by contexts of one type.</param>
        public Database(ScyllaConnection connection, ModelBuilder? model = null)
            : this(connection, model, ownsConnection: false)
        {
        }

        /// <summary>
        /// Standalone use (tools, tests): opens a connection of its own and
        /// closes it on dispose. Applications should share one connection.
        /// </summary>
        public Database(IScyllaOptions settings, IQueryLogger? logger = null)
            : this(new ScyllaConnection(settings, logger), model: null, ownsConnection: true)
        {
        }

        internal Database(ScyllaConnection connection, ModelBuilder? model, bool ownsConnection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _ownsConnection = ownsConnection;
            ModelBuilder = model ?? new ModelBuilder();

            var cqlGenerator = new CqlGenerator();
            _directStrategy = new DirectSaveStrategy(cqlGenerator, Executor, GetBuilder);
            _batchStrategy = new BatchSaveStrategy(cqlGenerator, GetBuilder);
        }

        #region Entity Operations

        public void Insert<T>(T entity, WriteOptions? options = null)
            => TrackEntity(entity, EntityState.Added, typeof(T), options);

        public void Update<T>(T entity, WriteOptions? options = null)
            => TrackEntity(entity, EntityState.Modified, typeof(T), options);

        public void Delete<T>(T entity, WriteOptions? options = null)
            => TrackEntity(entity, EntityState.Deleted, typeof(T), options);

        /// <summary>
        /// Deletes the stored values of the given columns on save. A null
        /// property alone never clears a value: it means "leave unchanged".
        /// </summary>
        public void ClearColumns<T>(T entity, params Expression<Func<T, object?>>[] columns)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            if (columns.Length == 0)
                throw new ArgumentException("At least one column is required.", nameof(columns));

            var names = columns.Select(PropertyName).ToList();

            lock (_sync)
            {
                if (!_changeTracker.TryGetValue(entity, out var tracked))
                {
                    tracked = new TrackedEntity(typeof(T), entity, EntityState.Modified);
                    _changeTracker[entity] = tracked;
                }
                else if (tracked.State == EntityState.Unchanged)
                {
                    tracked.State = EntityState.Modified;
                }

                tracked.ClearedColumns.UnionWith(names);
            }
        }

        private void TrackEntity(object? entity, EntityState state, Type type, WriteOptions? options)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            options?.Validate();

            lock (_sync)
            {
                if (_changeTracker.TryGetValue(entity, out var tracked))
                {
                    tracked.State = state;
                    tracked.Options = options ?? tracked.Options;
                    // An explicit Update writes the whole entity.
                    tracked.Changed = null;
                }
                else
                {
                    _changeTracker[entity] = new TrackedEntity(type, entity, state) { Options = options };
                }
            }
        }

        #region Tracking

        /// <summary>
        /// Loaded entities, identity-resolved: a row already tracked by this
        /// context comes back as the instance it has (its values kept, as EF
        /// does); a new one is tracked with a snapshot, so changes to it are
        /// found and saved by the next SaveChanges.
        /// </summary>
        internal List<T> AttachAll<T>(List<T> loaded)
        {
            var builder = ModelBuilder.GetBuilder(typeof(T));
            if (builder is null)
                return loaded;

            lock (_sync)
            {
                for (var i = 0; i < loaded.Count; i++)
                {
                    var entity = loaded[i];
                    if (entity is null)
                        continue;
                    var identity = (typeof(T), KeyOf(entity, builder));
                    if (_identities.TryGetValue(identity, out var existing))
                    {
                        loaded[i] = (T)existing;
                        continue;
                    }
                    _identities[identity] = entity;
                    _changeTracker[entity] = new TrackedEntity(typeof(T), entity, EntityState.Unchanged)
                    {
                        Original = Snapshot(entity, builder),
                    };
                }
            }
            return loaded;
        }

        /// <summary>Entities tracked now and their state (Added, Modified, Deleted, Unchanged).</summary>
        public IReadOnlyList<(object Entity, EntityState State)> Entries()
        {
            lock (_sync)
            {
                DetectChanges();
                return _changeTracker.Values.Select(t => (t.Entity, t.State)).ToList();
            }
        }

        // Loaded entities changed since their snapshot become Modified, with only those properties.
        private void DetectChanges()
        {
            foreach (var tracked in _changeTracker.Values)
            {
                // Unchanged, or found changed before (its set may have grown since).
                var detected = tracked.State == EntityState.Unchanged
                               || (tracked.State == EntityState.Modified && tracked.Changed is not null);
                if (!detected || tracked.Original is null)
                    continue;
                var builder = GetBuilder(tracked.EntityType);
                var changed = tracked.Original
                    .Where(o => !builder.GetPrimaryKey().Contains(o.Key)
                                && !Same(o.Value, Reflect.Property(tracked.EntityType, o.Key).GetValue(tracked.Entity)))
                    .Select(o => o.Key)
                    .ToHashSet(StringComparer.Ordinal);
                if (changed.Count == 0)
                    continue;
                tracked.State = EntityState.Modified;
                tracked.Changed = changed;
            }
        }

        private static Dictionary<string, object?> Snapshot(object entity, IEntityBuilder builder)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var name in builder.GetMappedProperties().Concat(builder.GetPrimaryKey()).Distinct())
            {
                var property = entity.GetType().GetProperty(name);
                if (property is not { CanRead: true })
                    continue;
                values[name] = Copy(property.GetValue(entity));
            }
            return values;
        }

        // Collections are copied, so changing one in place is seen as a change.
        private static object? Copy(object? value) => value switch
        {
            null or string => value,
            System.Collections.IDictionary map => map.Keys.Cast<object>()
                .Select(key => new KeyValuePair<object, object?>(key, map[key])).ToList(),
            System.Collections.IEnumerable items => items.Cast<object?>().ToList(),
            _ => value,
        };

        private static bool Same(object? original, object? current)
        {
            var copy = Copy(current);
            return (original, copy) switch
            {
                (null, null) => true,
                (null, _) or (_, null) => false,
                (List<KeyValuePair<object, object?>> a, List<KeyValuePair<object, object?>> b) => a.SequenceEqual(b),
                (List<object?> a, List<object?> b) => a.SequenceEqual(b),
                _ => original.Equals(copy),
            };
        }

        private static string KeyOf(object entity, IEntityBuilder builder)
            => string.Join('\u001f', builder.GetPrimaryKey().Select(name => Reflect.Property(entity.GetType(), name).GetValue(entity) switch
            {
                null => "\u0000",
                DateTimeOffset moment => moment.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                var value => value.ToString(),
            }));

        #endregion

        /// <summary>Stops tracking the entity: its pending change is dropped (e.g. after a conflict).</summary>
        public void Detach<T>(T entity)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));
            lock (_sync)
            {
                if (_changeTracker.Remove(entity, out var tracked))
                    ForgetIdentity(tracked);
            }
        }

        private void ForgetIdentity(TrackedEntity tracked)
        {
            var builder = ModelBuilder.GetBuilder(tracked.EntityType);
            if (builder is null)
                return;
            var key = (tracked.EntityType, KeyOf(tracked.Entity, builder));
            if (_identities.TryGetValue(key, out var known) && ReferenceEquals(known, tracked.Entity))
                _identities.Remove(key);
        }

        /// <summary>
        /// Writes one entity now, as a lightweight transaction; true when it
        /// applied. Its concurrency token, if any, is checked and renewed too.
        /// Not tracked and not part of the current batch.
        /// </summary>
        internal async Task<bool> WriteConditionallyAsync<T>(T entity, EntityState state, WriteCondition? condition,
            WriteOptions? options, CancellationToken ct)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));
            options?.Validate();
            var tracked = new TrackedEntity(typeof(T), entity, state) { Condition = condition, Options = options };
            await _directStrategy.ExecuteAsync([tracked], null, ct);
            return !tracked.Conflicted;
        }

        private static string PropertyName<T>(Expression<Func<T, object?>> expression)
            => expression.Body switch
            {
                MemberExpression member => member.Member.Name,
                UnaryExpression { Operand: MemberExpression member } => member.Member.Name,
                _ => throw new ArgumentException("Expected a property access, e.g. x => x.Name.", nameof(expression))
            };

        internal IEntityBuilder GetBuilder(Type entityType)
        {
            lock (_sync)
            {
                if (!_builderCache.TryGetValue(entityType, out var builder))
                {
                    builder = ModelBuilder.GetBuilder(entityType)
                        ?? throw new InvalidOperationException(
                            $"Entity {entityType.Name} is not configured. Call builder.Entity<{entityType.Name}>() in OnModelBuilding.");
                    _builderCache[entityType] = builder;
                }

                return builder;
            }
        }

        #endregion

        #region Batches & SaveChanges

        internal Batch? CurrentBatch
        {
            get
            {
                lock (_sync)
                    return _activeBatches.Count > 0 ? _activeBatches.Peek() : null;
            }
        }

        internal void PushActiveBatch(Batch batch)
        {
            lock (_sync)
                _activeBatches.Push(batch);
        }

        internal void PopActiveBatch()
        {
            lock (_sync)
            {
                if (_activeBatches.Count > 0)
                    _activeBatches.Pop();
            }
        }

        /// <summary>
        /// Collects the next saves into one batch until it is committed.
        /// Unlogged by default: use it for several writes to the same partition;
        /// pass <see cref="BatchType.Logged"/> when writes to different
        /// partitions must all eventually apply together.
        /// </summary>
        public Batch BeginBatch(BatchType batchType = BatchType.Unlogged)
            => new Batch(this, batchType);

        [Obsolete("A CQL batch is not a transaction. Use BeginBatch; pass BatchType.Logged for atomic multi-partition writes.")]
        public Batch BeginTransaction(BatchType batchType = BatchType.Logged)
            => BeginBatch(batchType);

        public int SaveChanges()
        {
            var batch = CurrentBatch;
            var snapshot = TakeSnapshot();
            if (snapshot.Count == 0) return 0;

            var count = StrategyFor(batch).Execute(snapshot.Select(s => s.Entity), batch);
            Complete(snapshot);
            ThrowIfConflicted(snapshot);
            return count;
        }

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            var batch = CurrentBatch;
            var snapshot = TakeSnapshot();
            if (snapshot.Count == 0) return 0;

            var count = await StrategyFor(batch).ExecuteAsync(snapshot.Select(s => s.Entity), batch, ct);
            Complete(snapshot);
            ThrowIfConflicted(snapshot);
            return count;
        }

        // Conflicted entities stay tracked (Complete skips them): reload, or detach.
        private static void ThrowIfConflicted(List<(TrackedEntity Entity, int Version)> snapshot)
        {
            var conflicted = snapshot.Where(s => s.Entity.Conflicted).Select(s => s.Entity.Entity).ToList();
            if (conflicted.Count > 0)
                throw new ScyllaConcurrencyException(conflicted);
        }

        // Chosen per call instead of swapped on push/pop, so a save cannot pick
        // up a strategy that belongs to another caller's batch.
        private ISaveStrategy StrategyFor(Batch? batch)
            => batch != null ? _batchStrategy : _directStrategy;

        /// <summary>
        /// Raised by SaveChanges before anything is written, with the entities
        /// about to be (like EF's SavingChanges / SaveChangesInterceptor): set
        /// audit fields, check invariants. Changes made to the entities here are
        /// saved; throwing cancels the save.
        /// </summary>
        public event Action<IReadOnlyList<(object Entity, EntityState State)>>? SavingChanges;

        private List<(TrackedEntity Entity, int Version)> TakeSnapshot()
        {
            List<TrackedEntity> pending;
            lock (_sync)
            {
                DetectChanges();
                pending = _changeTracker.Values.Where(t => t.State != EntityState.Unchanged).ToList();
            }

            if (pending.Count > 0 && SavingChanges is { } saving)
            {
                saving(pending.Select(t => (t.Entity, t.State)).ToList());
                lock (_sync)
                    DetectChanges(); // what the handlers changed on loaded entities
            }

            lock (_sync)
                return _changeTracker.Values
                    .Where(t => t.State != EntityState.Unchanged)
                    .Select(t => (t, t.Version))
                    .ToList();
        }

        /// <summary>
        /// Stops tracking what was saved. Entities changed again during the save
        /// (or added meanwhile) stay tracked for the next one; on failure nothing
        /// is removed, so the save can be retried.
        /// </summary>
        private void Complete(List<(TrackedEntity Entity, int Version)> snapshot)
        {
            lock (_sync)
            {
                foreach (var (tracked, version) in snapshot)
                {
                    if (tracked.Version != version || tracked.Conflicted)
                        continue;
                    if (tracked.State == EntityState.Deleted)
                    {
                        _changeTracker.Remove(tracked.Entity);
                        ForgetIdentity(tracked);
                        continue;
                    }

                    // Saved: tracked on as unchanged, from the values just written.
                    var builder = GetBuilder(tracked.EntityType);
                    tracked.State = EntityState.Unchanged;
                    tracked.Original = Snapshot(tracked.Entity, builder);
                    tracked.Changed = null;
                    tracked.Options = null;
                    tracked.Condition = null;
                    tracked.ClearedColumns.Clear();
                    _identities[(tracked.EntityType, KeyOf(tracked.Entity, builder))] = tracked.Entity;
                }
            }
        }

        #endregion

        #region Queries

        public IEnumerable<TResult> ExecuteQuery<TResult>(
            string cql,
            Func<Row, TResult>? projector = null,
            params object[] parameters)
            => Executor.Execute(cql, projector, parameters);

        public Task<IEnumerable<TResult>> ExecuteQueryAsync<TResult>(
            string cql,
            Func<Row, TResult>? projector = null,
            params object[] parameters)
            => Executor.ExecuteAsync(cql, projector, parameters);

        public Task<IEnumerable<TResult>> ExecuteQueryAsync<TResult>(
            string cql,
            Func<Row, TResult>? projector,
            object[] parameters,
            CancellationToken ct)
            => Executor.ExecuteAsync(cql, projector, parameters, ct);

        public IEnumerable<Row> ExecuteQuery(string cql, params object[] parameters)
            => Executor.Execute(cql, parameters);

        public Task<IEnumerable<Row>> ExecuteQueryAsync(string cql, params object[] parameters)
            => Executor.ExecuteAsync(cql, parameters);

        public Task<IEnumerable<Row>> ExecuteQueryAsync(string cql, object[] parameters, CancellationToken ct)
            => Executor.ExecuteAsync(cql, parameters, ct);

        public TResult? ExecuteScalar<TResult>(string cql, params object[] parameters)
            => Executor.ExecuteScalar<TResult>(cql, parameters);

        public Task<TResult?> ExecuteScalarAsync<TResult>(string cql, params object[] parameters)
            => Executor.ExecuteScalarAsync<TResult>(cql, parameters);

        public Task<TResult?> ExecuteScalarAsync<TResult>(string cql, object[] parameters, CancellationToken ct)
            => Executor.ExecuteScalarAsync<TResult>(cql, parameters, ct);

        public void ExecuteNonQuery(string cql, params object[] parameters)
            => Executor.ExecuteNonQuery(cql, parameters);

        public Task ExecuteNonQueryAsync(string cql, params object[] parameters)
            => Executor.ExecuteNonQueryAsync(cql, parameters);

        public Task ExecuteNonQueryAsync(string cql, object[] parameters, CancellationToken ct)
            => Executor.ExecuteNonQueryAsync(cql, parameters, ct);

        #endregion

        #region Schema

        /// <summary>
        /// Compares the configured model with the live tables (system_schema):
        /// missing tables or columns, wrong key layout, incompatible types.
        /// Run at startup to fail fast instead of on the first mapped row.
        /// </summary>
        public Task<IReadOnlyList<SchemaProblem>> ValidateSchemaAsync(CancellationToken ct = default)
            => new SchemaValidator(Executor, _connection.Keyspace).ValidateAsync(ModelBuilder.GetAllBuilders(), ct);

        #endregion

        #region Migrations

        public async Task<IEnumerable<IMigration>> GetPendingMigrationsAsync(CancellationToken ct = default)
        {
            var applied = await ExecuteQueryAsync(
                $"SELECT migration_id FROM {ScyllaConnection.MigrationsTable}",
                row => row.GetValue<string>("migration_id"),
                [],
                ct);

            var appliedSet = applied.ToHashSet();
            return _connection.Migrations.Where(m => !appliedSet.Contains(m.Id));
        }

        /// <summary>
        /// Applies pending migrations under a cluster-wide lock, so instances
        /// starting together do not run the same migration twice.
        /// </summary>
        public async Task MigrateAsync(CancellationToken ct = default)
        {
            var owner = Guid.NewGuid();
            await AcquireMigrationLockAsync(owner, ct);

            try
            {
                // Re-read under the lock: another instance may have just migrated.
                var pendingMigrations = (await GetPendingMigrationsAsync(ct)).ToList();

                foreach (var migration in pendingMigrations)
                    await ApplyAsync(migration, ct);
            }
            finally
            {
                await ReleaseMigrationLockAsync(owner);
            }
        }

        private async Task ApplyAsync(IMigration migration, CancellationToken ct)
        {
            try
            {
                await migration.Up(this, ct);

                if (!await migration.Validate(this, ct))
                    throw new InvalidOperationException($"Validation failed for migration {migration.Id}");

                await Executor.ExecuteNonQueryAsync(
                    $"INSERT INTO {ScyllaConnection.MigrationsTable} (migration_id, applied_at) VALUES (?, ?)",
                    [migration.Id, TimeProvider.GetUtcNow().UtcDateTime],
                    idempotent: true,
                    ct);
            }
            catch (Exception ex)
            {
                // DDL is not transactional: Down must undo a partial Up. If it
                // fails too, report both rather than hiding the original error.
                try
                {
                    await migration.Down(this, CancellationToken.None);
                }
                catch (Exception downEx)
                {
                    throw new AggregateException(
                        $"Migration {migration.Id} failed and rolling it back failed too; the schema may be partially migrated.",
                        ex, downEx);
                }

                throw;
            }
        }

        private async Task AcquireMigrationLockAsync(Guid owner, CancellationToken ct)
        {
            var deadline = TimeProvider.GetUtcNow().UtcDateTime + MigrationLockWait;

            while (true)
            {
                // LWT: exactly one instance gets [applied] = true. The TTL frees
                // the lock if its holder crashes mid-migration.
                var rows = await Executor.ExecuteAsync<Row>(
                    $"INSERT INTO {ScyllaConnection.MigrationLockTable} (lock_id, owner, acquired_at) " +
                    $"VALUES (?, ?, ?) IF NOT EXISTS USING TTL {(int)MigrationLockTtl.TotalSeconds}",
                    null,
                    [MigrationLockId, owner, TimeProvider.GetUtcNow().UtcDateTime],
                    idempotent: false,
                    ct);

                if (rows.FirstOrDefault()?.GetValue<bool>("[applied]") == true)
                    return;

                if (TimeProvider.GetUtcNow().UtcDateTime >= deadline)
                    throw new TimeoutException("Timed out waiting for the Scylla migration lock held by another instance.");

                await Task.Delay(MigrationLockPoll, ct);
            }
        }

        private async Task ReleaseMigrationLockAsync(Guid owner)
        {
            // Always runs, even after cancellation: a held lock would block every
            // other instance until its TTL expires.
            await Executor.ExecuteNonQueryAsync(
                $"DELETE FROM {ScyllaConnection.MigrationLockTable} WHERE lock_id = ? IF owner = ?",
                [MigrationLockId, owner],
                idempotent: false,
                CancellationToken.None);
        }

        public void ApplyMigration(Action<MigrationBuilder> buildAction)
        {
            // The connection's keyspace, so migrations need not hard-code one.
            var migration = new MigrationBuilder(_connection.Keyspace);
            buildAction(migration);

            foreach (var command in migration.GetCommands())
            {
                ExecuteNonQuery(command);
            }
        }

        public async Task ApplyMigrationAsync(Action<MigrationBuilder> buildAction, CancellationToken ct = default)
        {
            // The connection's keyspace, so migrations need not hard-code one.
            var migration = new MigrationBuilder(_connection.Keyspace);
            buildAction(migration);

            foreach (var command in migration.GetCommands())
            {
                await ExecuteNonQueryAsync(command, [], ct);
            }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_sync)
            {
                _changeTracker.Clear();
                _activeBatches.Clear();
            }

            if (_ownsConnection)
                _connection.Dispose();
        }
    }
}
