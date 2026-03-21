// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using System.Reflection;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Builders
{
    public class EntityBuilder<TEntity> : IEntityBuilder<TEntity> where TEntity : class
    {
        private readonly Dictionary<string, string> _columnMappings = new();
        private readonly Dictionary<string, Type> _columnTypes = new();
        private readonly List<string> _partitionKeys = new();
        private readonly List<string> _clusteringKeys = new();
        private readonly HashSet<string> _ignoredProperties = new();

        #region Table Configuration

        public string? TableName { get; private set; }
        public string? Keyspace { get; private set; }

        public IEntityBuilder<TEntity> ToTable(string name, string? keyspace = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Table name cannot be null or empty", nameof(name));

            TableName = name;
            Keyspace = keyspace;
            return this;
        }

        public string GetTableName() => TableName ?? typeof(TEntity).Name.ToLowerInvariant();

        public string GetFullTableName() =>
            string.IsNullOrEmpty(Keyspace)
                ? GetTableName()
                : $"{Keyspace}.{GetTableName()}";

        #endregion

        #region Property Configuration

        public PropertyBuilder<TEntity, TProp> Property<TProp>(Expression<Func<TEntity, TProp>> propertyExpression)
        {
            var propertyName = GetPropertyName(propertyExpression);
            return new PropertyBuilder<TEntity, TProp>(this, propertyName);
        }

        public IEntityBuilder<TEntity> Ignore<TProp>(Expression<Func<TEntity, TProp>> propertyExpression)
        {
            var propertyName = GetPropertyName(propertyExpression);
            _ignoredProperties.Add(propertyName);
            return this;
        }

        public bool IsIgnored(string propertyName) => _ignoredProperties.Contains(propertyName);

        internal void SetColumnName(string propertyName, string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException("Column name cannot be null or empty", nameof(columnName));

            _columnMappings[propertyName] = columnName;
        }

        internal void SetColumnType(string propertyName, Type type)
        {
            _columnTypes[propertyName] = type;
        }

        public string GetColumn(string propertyName)
        {
            if (_ignoredProperties.Contains(propertyName))
                throw new InvalidOperationException($"Property {propertyName} is ignored");

            return _columnMappings.TryGetValue(propertyName, out var columnName)
                ? columnName
                : propertyName.ToLowerInvariant();
        }

        public Type? GetColumnType(string propertyName) =>
            _columnTypes.TryGetValue(propertyName, out var type) ? type : null;

        #endregion

        #region Primary Key Configuration

        public IEntityBuilder<TEntity> HasKey<TKey>(Expression<Func<TEntity, TKey>> keyExpression)
        {
            var propertyNames = ExtractPropertyNames(keyExpression);

            _partitionKeys.Clear();
            _clusteringKeys.Clear();
            _partitionKeys.AddRange(propertyNames);

            return this;
        }

        public IEntityBuilder<TEntity> HasPartitionKey<TKey>(params Expression<Func<TEntity, object>>[] keyExpressions)
        {
            _partitionKeys.Clear();

            foreach (var expr in keyExpressions)
            {
                var propertyNames = ExtractPropertyNames(expr);
                _partitionKeys.AddRange(propertyNames);
            }

            return this;
        }

        public IEntityBuilder<TEntity> HasClusteringKey<TKey>(params Expression<Func<TEntity, object>>[] keyExpressions)
        {
            _clusteringKeys.Clear();

            foreach (var expr in keyExpressions)
            {
                var propertyNames = ExtractPropertyNames(expr);
                _clusteringKeys.AddRange(propertyNames);
            }

            return this;
        }

        public IReadOnlyList<string> GetPrimaryKey() =>
            _partitionKeys.Concat(_clusteringKeys).ToList().AsReadOnly();

        public IReadOnlyList<string> GetPartitionKey() => _partitionKeys.AsReadOnly();

        public IReadOnlyList<string> GetClusteringKey() => _clusteringKeys.AsReadOnly();

        #endregion

        #region Property Metadata

        public IReadOnlyDictionary<string, string> GetAllColumnMappings() =>
            _columnMappings.AsReadOnly();

        public IEnumerable<string> GetMappedProperties() =>
            typeof(TEntity)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && !_ignoredProperties.Contains(p.Name))
                .Select(p => p.Name);

        #endregion

        #region Validation

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(TableName))
                TableName = typeof(TEntity).Name.ToLowerInvariant();

            if (!_partitionKeys.Any())
                throw new InvalidOperationException(
                    $"Entity {typeof(TEntity).Name} must have at least one partition key defined");

            var properties = typeof(TEntity)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToHashSet();

            foreach (var key in _partitionKeys.Concat(_clusteringKeys))
            {
                if (!properties.Contains(key))
                    throw new InvalidOperationException(
                        $"Key property '{key}' not found in entity {typeof(TEntity).Name}");
            }
        }

        #endregion

        #region Helper Methods

        private static string GetPropertyName<TProp>(Expression<Func<TEntity, TProp>> propertyExpression)
        {
            return propertyExpression.Body switch
            {
                MemberExpression member => member.Member.Name,
                UnaryExpression { Operand: MemberExpression member } => member.Member.Name,
                _ => throw new ArgumentException(
                    "Expression must be a property access (e.g., x => x.Property)",
                    nameof(propertyExpression))
            };
        }

        private static List<string> ExtractPropertyNames<TKey>(Expression<Func<TEntity, TKey>> keyExpression)
        {
            return keyExpression.Body switch
            {
                MemberExpression member => new List<string> { member.Member.Name },

                UnaryExpression { Operand: MemberExpression member } =>
                    new List<string> { member.Member.Name },

                NewExpression newExpr when newExpr.Members != null =>
                    newExpr.Members.Select(m => m.Name).ToList(),

                _ => throw new ArgumentException(
                    "Expression must be a property access or anonymous object (e.g., x => x.Id or x => new { x.Id, x.TenantId })",
                    nameof(keyExpression))
            };
        }

        #endregion
    }
}
