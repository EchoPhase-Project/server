// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Linq.Expressions;
using Cassandra;
using EchoPhase.DAL.Scylla.Cql;
using EchoPhase.DAL.Scylla.Enums;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Linq;
using EchoPhase.DAL.Scylla.Models;



namespace EchoPhase.DAL.Scylla.Database
{
    public class DbSet<TEntity> : IQueryable<TEntity> where TEntity : class, new()
    {
        private readonly Database _database;
        private readonly QueryProvider _provider;

        public DbSet(IQueryProviderFactory factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _provider = factory.Create();
            _database = _provider.Database;
            Expression = Expression.Constant(this);
        }

        internal DbSet(QueryProvider provider, Expression expression)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _database = provider.Database;
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        #region IQueryable Implementation

        public IEnumerator<TEntity> GetEnumerator() =>
            _provider.Execute<IEnumerable<TEntity>>(Expression).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public Type ElementType => typeof(TEntity);

        public Expression Expression { get; }

        public IQueryProvider Provider => _provider;

        #endregion

        #region CRUD Operations

        public void Add(TEntity entity, WriteOptions? options = null)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _database.Insert(entity, options);
        }

        public void AddRange(IEnumerable<TEntity> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            foreach (var entity in entities)
            {
                _database.Insert(entity);
            }
        }

        public void AddRange(params TEntity[] entities)
        {
            AddRange((IEnumerable<TEntity>)entities);
        }

        /// <summary>
        /// Writes every non-null property. Null properties are left unchanged in
        /// the database; use <see cref="ClearColumns"/> to delete stored values.
        /// </summary>
        public void Update(TEntity entity, WriteOptions? options = null)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _database.Update(entity, options);
        }

        public void UpdateRange(IEnumerable<TEntity> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            foreach (var entity in entities)
            {
                _database.Update(entity);
            }
        }

        public void UpdateRange(params TEntity[] entities)
        {
            UpdateRange((IEnumerable<TEntity>)entities);
        }

        /// <param name="options">Only <see cref="WriteOptions.Timestamp"/> applies to deletes.</param>
        public void Remove(TEntity entity, WriteOptions? options = null)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _database.Delete(entity, options);
        }

        public void RemoveRange(IEnumerable<TEntity> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            foreach (var entity in entities)
            {
                _database.Delete(entity);
            }
        }

        public void RemoveRange(params TEntity[] entities)
        {
            RemoveRange((IEnumerable<TEntity>)entities);
        }

        /// <summary>
        /// Deletes the stored values of the given columns on the next save
        /// (<c>DELETE col, ... FROM ... WHERE key</c>).
        /// </summary>
        public void ClearColumns(TEntity entity, params Expression<Func<TEntity, object?>>[] columns)
            => _database.ClearColumns(entity, columns);

        /// <summary>Stops tracking the entity; its pending change is dropped.</summary>
        public void Detach(TEntity entity) => _database.Detach(entity);

        #endregion

        #region Lightweight transactions

        /// <summary>
        /// Inserts the row now only if it does not exist (INSERT … IF NOT EXISTS):
        /// true when it was inserted. A claim, a lock, a unique name.
        /// </summary>
        public Task<bool> AddIfNotExistsAsync(TEntity entity, WriteOptions? options = null, CancellationToken ct = default)
            => _database.WriteConditionallyAsync(entity, EntityState.Added, WriteCondition.NotExists, options, ct);

        /// <summary>
        /// Updates the row now only if <paramref name="condition"/> holds on the
        /// stored values (null: only if the row exists): true when it applied.
        /// E.g. a status change that must not race: <c>r =&gt; r.Status == "queued"</c>.
        /// </summary>
        public Task<bool> UpdateIfAsync(TEntity entity, Expression<Func<TEntity, bool>>? condition,
            WriteOptions? options = null, CancellationToken ct = default)
            => _database.WriteConditionallyAsync(entity, EntityState.Modified,
                condition is null ? WriteCondition.Exists : ConditionTranslator.Translate(condition), options, ct);

        /// <summary>Deletes the row now only if <paramref name="condition"/> holds (null: if it exists): true when it applied.</summary>
        public Task<bool> RemoveIfAsync(TEntity entity, Expression<Func<TEntity, bool>>? condition = null,
            CancellationToken ct = default)
            => _database.WriteConditionallyAsync(entity, EntityState.Deleted,
                condition is null ? WriteCondition.Exists : ConditionTranslator.Translate(condition), null, ct);

        #endregion

        #region Query Methods

        public TEntity? Find(params object[] keyValues)
        {
            var (cql, parameters) = BuildFind(keyValues);
            return _database.Executor.Execute(cql, MapRowToEntity, parameters, idempotent: true).FirstOrDefault();
        }

        public Task<TEntity?> FindAsync(params object[] keyValues)
            => FindAsync(keyValues, CancellationToken.None);

        public async Task<TEntity?> FindAsync(object[] keyValues, CancellationToken ct)
        {
            var (cql, parameters) = BuildFind(keyValues);
            var results = await _database.Executor.ExecuteAsync(cql, MapRowToEntity, parameters, idempotent: true, ct);
            return results.FirstOrDefault();
        }

        private (string cql, object[] parameters) BuildFind(object[] keyValues)
        {
            if (keyValues == null || keyValues.Length == 0)
                throw new ArgumentException("Key values must be provided", nameof(keyValues));

            var builder = Builder;

            var pkNames = builder.GetPrimaryKey();
            if (pkNames == null || pkNames.Count == 0)
                throw new InvalidOperationException($"No primary key defined for type {typeof(TEntity).Name}");

            if (pkNames.Count != keyValues.Length)
                throw new InvalidOperationException(
                    $"Expected {pkNames.Count} key values, but got {keyValues.Length}");

            var whereClause = string.Join(" AND ",
                pkNames.Select(pk => $"{builder.GetColumn(pk)} = ?"));

            return ($"SELECT * FROM {builder.GetFullTableName()} WHERE {whereClause} LIMIT 1", keyValues);
        }

        // The methods below go through the same LINQ provider as any query, so a
        // DbSet and a filtered query behave the same; they exist for discoverability.

        public List<TEntity> ToList() => Enumerable.ToList(this);

        public Task<List<TEntity>> ToListAsync(CancellationToken ct = default)
            => ScyllaQueryableExtensions.ToListAsync(this, ct);

        public TEntity? FirstOrDefault() => Queryable.FirstOrDefault(this);

        public Task<TEntity?> FirstOrDefaultAsync(CancellationToken ct = default)
            => ScyllaQueryableExtensions.FirstOrDefaultAsync(this, ct);

        public long Count() => Queryable.LongCount(this);

        public Task<long> CountAsync(CancellationToken ct = default)
            => ScyllaQueryableExtensions.LongCountAsync(this, ct);

        public bool Any() => Queryable.Any(this);

        public Task<bool> AnyAsync(CancellationToken ct = default)
            => ScyllaQueryableExtensions.AnyAsync(this, ct);

        #endregion

        #region Helper Methods

        private IEntityBuilder Builder =>
            _database.ModelBuilder.GetBuilder<TEntity>()
            ?? throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

        private TEntity MapRowToEntity(Row row)
        {
            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            return builder == null ? new TEntity() : EntityMapper.Map<TEntity>(row, builder);
        }

        #endregion

        #region Raw Query Execution

        public IEnumerable<TEntity> FromSql(string cql, params object[] parameters)
        {
            return _database.ExecuteQuery<TEntity>(cql, MapRowToEntity, parameters);
        }

        public Task<IEnumerable<TEntity>> FromSqlAsync(string cql, params object[] parameters)
            => FromSqlAsync(cql, parameters, CancellationToken.None);

        public Task<IEnumerable<TEntity>> FromSqlAsync(string cql, object[] parameters, CancellationToken ct)
            => _database.ExecuteQueryAsync<TEntity>(cql, MapRowToEntity, parameters, ct);

        #endregion
    }
}
