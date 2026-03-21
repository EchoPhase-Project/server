// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using Cassandra;
using EchoPhase.DAL.Scylla.Cql;
using EchoPhase.DAL.Scylla.Database;
using ScyllaDatabase = EchoPhase.DAL.Scylla.Database.Database;

namespace EchoPhase.DAL.Scylla.Linq
{
    public class QueryProvider : IQueryProvider
    {
        public readonly DbContext _context;
        public ScyllaDatabase Database => _context.Database;

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
