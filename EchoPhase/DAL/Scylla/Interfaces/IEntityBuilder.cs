using System.Linq.Expressions;

namespace EchoPhase.DAL.Scylla.Interfaces
{
    public interface IEntityBuilder
    {
        string GetTableName();
        string GetColumn(string propertyName);
        IReadOnlyList<string> GetPrimaryKey();
    }

    public interface IEntityBuilder<TEntity> : IEntityBuilder
    {
        IEntityBuilder<TEntity> ToTable(string name);
        PropertyBuilder<TEntity, TProperty> Property<TProperty>(Expression<Func<TEntity, TProperty>> property);
        IEntityBuilder<TEntity> HasKey<T>(Expression<Func<TEntity, T>> prop);
    }
}
