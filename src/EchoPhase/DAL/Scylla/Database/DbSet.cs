using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Cassandra;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla
{
    public class DbSet<TEntity> : IQueryable<TEntity> where TEntity : class, new()
    {
        private readonly Database _database;
        private readonly Expression _expression;
        private readonly QueryProvider _provider;

        public DbSet(IQueryProviderFactory factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _provider = factory.Create();
            _database = _provider.Database;
            _expression = Expression.Constant(this);
        }

        internal DbSet(QueryProvider provider, Expression expression)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _database = provider.Database;
            _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        #region IQueryable Implementation

        public IEnumerator<TEntity> GetEnumerator() =>
            _provider.Execute<IEnumerable<TEntity>>(_expression).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public Type ElementType => typeof(TEntity);

        public Expression Expression => _expression;

        public IQueryProvider Provider => _provider;

        #endregion

        #region CRUD Operations

        public void Add(TEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _database.Insert(entity);
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

        public void Update(TEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _database.Update(entity);
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

        public void Remove(TEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _database.Delete(entity);
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

        #endregion

        #region Query Methods

        public TEntity? Find(params object[] keyValues)
        {
            if (keyValues == null || keyValues.Length == 0)
                throw new ArgumentException("Key values must be provided", nameof(keyValues));

            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

            var pkNames = builder.GetPrimaryKey();
            if (pkNames == null || pkNames.Count == 0)
                throw new InvalidOperationException($"No primary key defined for type {typeof(TEntity).Name}");

            if (pkNames.Count != keyValues.Length)
                throw new InvalidOperationException(
                    $"Expected {pkNames.Count} key values, but got {keyValues.Length}");

            var whereClause = string.Join(" AND ",
                pkNames.Select(pk => $"{builder.GetColumn(pk)} = ?"));

            var cql = $"SELECT * FROM {builder.GetTableName()} WHERE {whereClause} LIMIT 1";

            var results = _database.ExecuteQuery<TEntity>(cql,
                row => MapRowToEntity(row),
                keyValues);

            return results.FirstOrDefault();
        }

        public async Task<TEntity?> FindAsync(params object[] keyValues)
        {
            if (keyValues == null || keyValues.Length == 0)
                throw new ArgumentException("Key values must be provided", nameof(keyValues));

            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

            var pkNames = builder.GetPrimaryKey();
            if (pkNames == null || pkNames.Count == 0)
                throw new InvalidOperationException($"No primary key defined for type {typeof(TEntity).Name}");

            if (pkNames.Count != keyValues.Length)
                throw new InvalidOperationException(
                    $"Expected {pkNames.Count} key values, but got {keyValues.Length}");

            var whereClause = string.Join(" AND ",
                pkNames.Select(pk => $"{builder.GetColumn(pk)} = ?"));

            var cql = $"SELECT * FROM {builder.GetTableName()} WHERE {whereClause} LIMIT 1";

            var results = await _database.ExecuteQueryAsync<TEntity>(cql,
                row => MapRowToEntity(row),
                keyValues);

            return results.FirstOrDefault();
        }

        public List<TEntity> ToList()
        {
            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

            var cql = $"SELECT * FROM {builder.GetTableName()}";
            return _database.ExecuteQuery<TEntity>(cql, row => MapRowToEntity(row)).ToList();
        }

        public async Task<List<TEntity>> ToListAsync()
        {
            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

            var cql = $"SELECT * FROM {builder.GetTableName()}";
            var results = await _database.ExecuteQueryAsync<TEntity>(cql, row => MapRowToEntity(row));
            return results.ToList();
        }

        public TEntity? FirstOrDefault()
        {
            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

            var cql = $"SELECT * FROM {builder.GetTableName()} LIMIT 1";
            return _database.ExecuteQuery<TEntity>(cql, row => MapRowToEntity(row)).FirstOrDefault();
        }

        public async Task<TEntity?> FirstOrDefaultAsync()
        {
            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

            var cql = $"SELECT * FROM {builder.GetTableName()} LIMIT 1";
            var results = await _database.ExecuteQueryAsync<TEntity>(cql, row => MapRowToEntity(row));
            return results.FirstOrDefault();
        }

        public long Count()
        {
            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

            var cql = $"SELECT COUNT(*) FROM {builder.GetTableName()}";
            return _database.ExecuteScalar<long>(cql);
        }

        public async Task<long> CountAsync()
        {
            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

            var cql = $"SELECT COUNT(*) FROM {builder.GetTableName()}";
            return await _database.ExecuteScalarAsync<long>(cql);
        }

        public bool Any()
        {
            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

            var cql = $"SELECT * FROM {builder.GetTableName()} LIMIT 1";
            return _database.ExecuteQuery(cql).Any();
        }

        public async Task<bool> AnyAsync()
        {
            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                throw new InvalidOperationException($"No model builder found for type {typeof(TEntity).Name}");

            var cql = $"SELECT * FROM {builder.GetTableName()} LIMIT 1";
            var results = await _database.ExecuteQueryAsync(cql);
            return results.Any();
        }

        #endregion

        #region Helper Methods

        private TEntity MapRowToEntity(Row row)
        {
            var entity = new TEntity();
            var type = typeof(TEntity);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(p => p.CanWrite);

            var builder = _database.ModelBuilder.GetBuilder<TEntity>();
            if (builder == null)
                return entity;

            foreach (var prop in properties)
            {
                try
                {
                    var columnName = builder.GetColumn(prop.Name);
                    if (string.IsNullOrEmpty(columnName))
                        continue;

                    var columnInfo = row.GetColumn(columnName);
                    if (columnInfo != null)
                    {
                        var value = row.GetValue(prop.PropertyType, columnName);
                        if (value != null && value != DBNull.Value)
                        {
                            prop.SetValue(entity, value);
                        }
                    }
                }
                catch
                {
                }
            }

            return entity;
        }

        #endregion

        #region Raw Query Execution

        public IEnumerable<TEntity> FromSql(string cql, params object[] parameters)
        {
            return _database.ExecuteQuery<TEntity>(cql, row => MapRowToEntity(row), parameters);
        }

        public async Task<IEnumerable<TEntity>> FromSqlAsync(string cql, params object[] parameters)
        {
            return await _database.ExecuteQueryAsync<TEntity>(cql, row => MapRowToEntity(row), parameters);
        }

        #endregion
    }
}
