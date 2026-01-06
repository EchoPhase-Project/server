using System.Collections;
using System.Linq.Expressions;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla
{
    public class DbSet<TEntity> : IQueryable<TEntity>
    {
        private readonly Expression _expression;
        private readonly QueryProvider _provider;

        public DbSet(IQueryProviderFactory factory)
        {
            _provider = factory.Create();
            _expression = Expression.Constant(this);
        }

        public DbSet(QueryProvider provider, Expression expression)
        {
            _provider = provider;
            _expression = expression;
        }

        public IEnumerator<TEntity> GetEnumerator() =>
            _provider.Execute<IEnumerable<TEntity>>(_expression).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public Type ElementType => typeof(TEntity);
        public Expression Expression => _expression;
        public IQueryProvider Provider => _provider;

        public void Add(TEntity entity)
        {
            _provider.Database.Insert(entity);
        }

        public void Update(TEntity entity)
        {
            _provider.Database.Update(entity);
        }

        public void Remove(TEntity entity)
        {
            _provider.Database.Delete(entity);
        }
    }
}
