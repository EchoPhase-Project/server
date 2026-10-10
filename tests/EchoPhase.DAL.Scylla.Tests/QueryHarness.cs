// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Linq.Expressions;
using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Cql;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Linq;

namespace EchoPhase.DAL.Scylla.Tests
{
    /// <summary>
    /// Builds LINQ expressions rooted at a DbSet constant, as a real query
    /// would be, and translates them without a database: the visitor only
    /// looks at the root's type.
    /// </summary>
    internal static class QueryHarness
    {
        public static EntityBuilder<AuditEntry> AuditModel()
        {
            var builder = new EntityBuilder<AuditEntry>();
            builder.ToTable("audit", "ks");
            builder.HasPartitionKey<object>(x => x.ImpersonationId);
            builder.HasClusteringKey<object>(x => x.At);
            builder.Property(x => x.ImpersonationId).HasColumnName("impersonation_id");
            builder.Validate();
            return builder;
        }

        public static IQueryable<T> Query<T>() => new CapturingQueryable<T>(Expression.Constant(null, typeof(DbSet<>).MakeGenericType(typeof(T))));

        /// <summary>Same as <c>.AllowFiltering()</c> on a real Scylla query.</summary>
        public static IQueryable<T> AllowFiltering<T>(IQueryable<T> source)
        {
            var method = typeof(ScyllaQueryableExtensions).GetMethod(nameof(ScyllaQueryableExtensions.AllowFiltering))!
                .MakeGenericMethod(typeof(T));
            return source.Provider.CreateQuery<T>(Expression.Call(null, method, source.Expression));
        }

        public static (string Cql, object[] Parameters) Translate<T>(IQueryable<T> query, IEntityBuilder builder)
        {
            var visitor = new ExpressionVisitor<T>(_ => builder);
            var cql = visitor.VisitExpression(query.Expression);
            return (cql, visitor.Parameters);
        }

        private sealed class CapturingQueryable<T>(Expression expression) : IOrderedQueryable<T>
        {
            public Type ElementType => typeof(T);
            public Expression Expression { get; } = expression;
            public IQueryProvider Provider { get; } = new CapturingProvider();
            public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class CapturingProvider : IQueryProvider
        {
            public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
                => new CapturingQueryable<TElement>(expression);

            public object? Execute(Expression expression) => throw new NotSupportedException();

            public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException();
        }
    }
}
