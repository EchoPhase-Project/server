// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using EchoPhase.DAL.Scylla.Cql;
using EchoPhase.DAL.Scylla.Database;
using ScyllaDatabase = EchoPhase.DAL.Scylla.Database.Database;

namespace EchoPhase.DAL.Scylla.Linq
{
    public class QueryProvider : IQueryProvider
    {
        private static readonly MethodInfo ExecuteMethod = typeof(QueryProvider)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == nameof(Execute) && m.IsGenericMethodDefinition);

        private static readonly MethodInfo ExecuteSequenceMethod = typeof(QueryProvider)
            .GetMethod(nameof(ExecuteSequence), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly HashSet<string> PredicateMethods = new()
        {
            "Count", "LongCount", "Any", "First", "FirstOrDefault", "Single", "SingleOrDefault"
        };

        private readonly DbContext _context;
        public ScyllaDatabase Database => _context.Database;

        public QueryProvider(DbContext dbContext)
        {
            _context = dbContext;
        }

        IQueryable IQueryProvider.CreateQuery(Expression expression)
        {
            var elementType = GetElementType(expression.Type);
            return (IQueryable)Activator.CreateInstance(
                typeof(ScyllaQueryable<>).MakeGenericType(elementType),
                this, expression)!;
        }

        IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression)
            => new ScyllaQueryable<TElement>(this, expression);

        public object? Execute(Expression expression) =>
            ExecuteMethod.MakeGenericMethod(expression.Type).Invoke(this, new object[] { expression });

        public TResult Execute<TResult>(Expression expression)
        {
            if (expression is MethodCallExpression call &&
                call.Method.DeclaringType == typeof(Queryable) &&
                !typeof(IQueryable).IsAssignableFrom(call.Type))
                return ExecuteTerminal<TResult>(call);

            return (TResult)ExecuteSequence(GetElementType(expression.Type), expression);
        }

        private TResult ExecuteTerminal<TResult>(MethodCallExpression call)
        {
            var name = call.Method.Name;
            var source = call.Arguments[0];
            var elementType = GetElementType(source.Type);
            var rest = call.Arguments.Skip(1).ToList();

            // Push predicates of Count(x => ...), First(x => ...) etc. into the CQL WHERE clause.
            if (PredicateMethods.Contains(name) && rest.Count == 1 &&
                StripQuotes(rest[0]) is LambdaExpression { Parameters.Count: 1, ReturnType: var returnType } &&
                returnType == typeof(bool))
            {
                source = Expression.Call(typeof(Queryable), nameof(Queryable.Where), new[] { elementType }, source, rest[0]);
                rest.Clear();
            }

            if (name is "Count" or "LongCount" && rest.Count == 0)
            {
                var visitor = new ExpressionVisitor<object>(Database.GetBuilder);
                var cql = visitor.VisitCount(source);
                var count = Database.Executor.Run(Statement(cql, visitor)).Rows
                    .Select(row => row.GetValue<long>(0))
                    .FirstOrDefault();

                // COUNT(*) yields a single row, so LIMIT would not cap it; apply Take() here instead.
                if (visitor.Limit.HasValue)
                    count = Math.Min(count, visitor.Limit.Value);

                return (TResult)Convert.ChangeType(count, typeof(TResult));
            }

            int? limit = name switch
            {
                "Any" or "First" or "FirstOrDefault" => 1,
                "Single" or "SingleOrDefault" => 2,
                _ => null
            };
            if (limit.HasValue)
                source = Expression.Call(typeof(Queryable), nameof(Queryable.Take), new[] { elementType }, source, Expression.Constant(limit.Value));

            // Everything else (aggregates, All, Last, ...) runs on the client over the fetched rows.
            var items = ExecuteSequence(elementType, source);

            if (limit is null)
                Database.Executor.Logger?.LogWarning(
                    $"Queryable.{name} is evaluated on the client over {((ICollection)items).Count} fetched row(s); " +
                    "narrow the query (Where/Take) or compute it in CQL if this is a hot path.");
            var local = Queryable.AsQueryable(items);
            var localCall = Expression.Call(call.Method, new[] { Expression.Constant(local) }.Concat(rest));
            return local.Provider.Execute<TResult>(localCall);
        }

        private IEnumerable ExecuteSequence(Type elementType, Expression expression) =>
            (IEnumerable)ExecuteSequenceMethod.MakeGenericMethod(elementType).Invoke(this, new object[] { expression })!;

        private List<T> ExecuteSequence<T>(Expression expression)
        {
            var visitor = new ExpressionVisitor<T>(Database.GetBuilder);
            var cql = visitor.VisitExpression(expression);

            if (visitor.Limit == 0)
                return new List<T>();

            var rows = Database.Executor.Run(Statement(cql, visitor), typeof(T)).Rows.Select(visitor.Projector!).ToList();
            return visitor.ReturnsTrackedEntities ? Database.AttachAll(rows) : rows;
        }

        internal async Task<List<T>> ExecuteSequenceAsync<T>(Expression expression, CancellationToken ct)
        {
            var visitor = new ExpressionVisitor<T>(Database.GetBuilder);
            var cql = visitor.VisitExpression(expression);

            if (visitor.Limit == 0)
                return new List<T>();

            var rows = (await Database.Executor.RunAsync(Statement(cql, visitor), typeof(T), ct)).Rows.Select(visitor.Projector!).ToList();
            return visitor.ReturnsTrackedEntities ? Database.AttachAll(rows) : rows;
        }

        internal async Task<long> CountAsync<T>(Expression expression, CancellationToken ct)
        {
            var visitor = new ExpressionVisitor<object>(Database.GetBuilder);
            var cql = visitor.VisitCount(expression);

            var result = await Database.Executor.RunAsync(Statement(cql, visitor), typeof(long), ct);
            var count = result.Rows.Select(row => row.GetValue<long>(0)).FirstOrDefault();

            // COUNT(*) yields a single row, so LIMIT would not cap it; apply Take() here instead.
            return visitor.Limit.HasValue ? Math.Min(count, visitor.Limit.Value) : count;
        }

        internal async Task<ScyllaPage<T>> ExecutePageAsync<T>(
            Expression expression, int pageSize, byte[]? pagingState, CancellationToken ct)
        {
            var visitor = new ExpressionVisitor<T>(Database.GetBuilder);
            var cql = visitor.VisitExpression(expression);

            if (visitor.Limit.HasValue)
                throw new NotSupportedException("ToPageAsync() pages through all matching rows; remove Take() and use pageSize.");

            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be positive.");
            var result = await Database.Executor.RunAsync(
                Statement(cql, visitor) with { PageSize = pageSize, PagingState = pagingState }, typeof(T), ct);
            var items = result.Rows.Select(visitor.Projector!).ToList();
            return new ScyllaPage<T>(visitor.ReturnsTrackedEntities ? Database.AttachAll(items) : items, result.PagingState);
        }

        internal async Task ExecuteUpdateAsync<T>(Expression expression, Action<UpdateSetters<T>> configure,
            Models.WriteOptions? options, CancellationToken ct)
        {
            var visitor = new ExpressionVisitor<T>(Database.GetBuilder);
            var (table, where) = visitor.VisitUpdateTarget(expression);
            var setters = new UpdateSetters<T>(visitor.EntityBuilder);
            configure(setters);
            if (setters.Assignments.Count == 0)
                throw new ArgumentException("Nothing to update: call Set, Increment, Append ...", nameof(configure));

            options?.Validate();
            var usingParts = new List<string>();
            var values = new List<object?>();
            if (options?.TtlSeconds is { } ttl)
            {
                usingParts.Add("TTL ?");
                values.Add(ttl);
            }
            if (options?.TimestampMicros is { } timestamp)
            {
                usingParts.Add("TIMESTAMP ?");
                values.Add(timestamp);
            }
            values.AddRange(setters.Assignments.SelectMany(a => a.Values));
            values.AddRange(visitor.Parameters);

            var usingClause = usingParts.Count > 0 ? $" USING {string.Join(" AND ", usingParts)}" : string.Empty;
            var cql = $"UPDATE {table}{usingClause} SET {string.Join(", ", setters.Assignments.Select(a => a.Clause))} WHERE {where}";
            await Database.Executor.RunAsync(
                new CqlStatement(cql, values.ToArray(), Idempotent: setters.Assignments.All(a => a.Idempotent)), null, ct);
        }

        // LINQ produces plain SELECTs, which are always safe to retry.
        private static CqlStatement Statement<T>(string cql, ExpressionVisitor<T> visitor)
            => new(cql, visitor.Parameters, Idempotent: true, Options: visitor.Options);

        private static Type GetElementType(Type sequenceType)
        {
            if (sequenceType.IsGenericType && sequenceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return sequenceType.GetGenericArguments()[0];

            return sequenceType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0]
                ?? throw new NotSupportedException($"Type {sequenceType.Name} is not a sequence");
        }

        private static Expression StripQuotes(Expression e) =>
            e.NodeType == ExpressionType.Quote ? ((UnaryExpression)e).Operand : e;
    }
}
