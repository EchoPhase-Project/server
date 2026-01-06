using System.Linq.Expressions;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla
{
    public class EntityBuilder<TEntity> : IEntityBuilder<TEntity>
    {
        private readonly Dictionary<string, string> _columnMappings = new();
        private readonly List<string> _primaryKeys = new();

        public string? TableName
        {
            get; private set;
        }

        public IEntityBuilder<TEntity> ToTable(string name)
        {
            TableName = name;
            return this;
        }

        public PropertyBuilder<TEntity, TProp> Property<TProp>(Expression<Func<TEntity, TProp>> prop)
        {
            var name = GetPropertyName(prop);
            return new PropertyBuilder<TEntity, TProp>(this, name);
        }

        public IEntityBuilder<TEntity> HasKey<T>(Expression<Func<TEntity, T>> prop)
        {
            if (prop.Body is MemberExpression member)
            {
                _primaryKeys.Add(member.Member.Name);
            }
            else if (prop.Body is UnaryExpression unary && unary.Operand is MemberExpression member2)
            {
                _primaryKeys.Add(member2.Member.Name);
            }
            else if (prop.Body is NewExpression newExpr)
            {
                foreach (var arg in newExpr.Members!)
                    _primaryKeys.Add(arg.Name);
            }
            else
            {
                throw new ArgumentException("Expression must be a property access or anonymous object", nameof(prop));
            }

            return this;
        }

        internal void SetColumnName(string propertyName, string columnName)
        {
            _columnMappings[propertyName] = columnName;
        }

        private string GetPropertyName<TProp>(Expression<Func<TEntity, TProp>> prop)
        {
            if (prop.Body is MemberExpression member) return member.Member.Name;
            if (prop.Body is UnaryExpression unary && unary.Operand is MemberExpression member2) return member2.Member.Name;
            throw new ArgumentException("Expression must be a property access", nameof(prop));
        }

        public string GetTableName() => TableName ?? typeof(TEntity).Name;

        public string GetColumn(string propertyName) => _columnMappings.TryGetValue(propertyName, out var col) ? col : propertyName;

        public IReadOnlyList<string> GetPrimaryKey() => _primaryKeys.AsReadOnly();
    }
}
