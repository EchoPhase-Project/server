using System.Linq.Expressions;
using Cassandra;
using EchoPhase.DAL.Scylla.Cql;

namespace EchoPhase.DAL.Scylla
{
    public class QueryProvider : IQueryProvider
    {
        public readonly DbContext _context;
        public Database Database => _context.Database;

        public QueryProvider(DbContext dbContext)
        {
            _context = dbContext;
        }

        IQueryable IQueryProvider.CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetGenericArguments()[0];
            return (IQueryable)Activator.CreateInstance(
                typeof(DbSet<>).MakeGenericType(elementType),
                this, expression)!;
        }

        IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
        {
            return (IQueryable<TElement>)Activator.CreateInstance(
                typeof(DbSet<>).MakeGenericType(typeof(TElement)),
                this, expression)!;
        }

        public object? Execute(Expression expression) => Execute<object>(expression);

        public TResult Execute<TResult>(Expression expression)
        {
            var cql = Translate<TResult>(expression, out var projector);
            return (TResult)Database.ExecuteQuery<TResult>(cql, projector);
        }

        private string Translate<TResult>(Expression expression, out Func<Row, TResult>? projector)
        {
            var visitor = new ExpressionVisitor<TResult>();
            var cql = visitor.VisitExpression(expression);
            projector = visitor.Projector;
            return cql;
        }
    }
}
