using System.Linq.Expressions;

namespace EchoPhase.DAL.Scylla.Interfaces
{
    public interface IEntityBuilder<TEntity> : IEntityBuilder where TEntity : class
    {
        IEntityBuilder<TEntity> ToTable(string name, string? keyspace = null);
        PropertyBuilder<TEntity, TProp> Property<TProp>(Expression<Func<TEntity, TProp>> propertyExpression);
        IEntityBuilder<TEntity> HasKey<TKey>(Expression<Func<TEntity, TKey>> keyExpression);
        IEntityBuilder<TEntity> HasPartitionKey<TKey>(params Expression<Func<TEntity, object>>[] keyExpressions);
        IEntityBuilder<TEntity> HasClusteringKey<TKey>(params Expression<Func<TEntity, object>>[] keyExpressions);
        IEntityBuilder<TEntity> Ignore<TProp>(Expression<Func<TEntity, TProp>> propertyExpression);
    }

    public interface IEntityBuilder
    {
        string GetTableName();
        string GetFullTableName();
        string GetColumn(string propertyName);
        Type? GetColumnType(string propertyName);
        IReadOnlyList<string> GetPrimaryKey();
        IReadOnlyList<string> GetPartitionKey();
        IReadOnlyList<string> GetClusteringKey();
        bool IsIgnored(string propertyName);
        IReadOnlyDictionary<string, string> GetAllColumnMappings();
        IEnumerable<string> GetMappedProperties();
        void Validate();
    }
}
