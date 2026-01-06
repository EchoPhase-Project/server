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

        public IQueryable CreateQuery(Expression expression) =>
            (IQueryable)Activator.CreateInstance(
                typeof(DbSet<>).MakeGenericType(expression.Type.GetGenericArguments()[0]),
                this, expression)!;

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new DbSet<TElement>(this, expression);

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
