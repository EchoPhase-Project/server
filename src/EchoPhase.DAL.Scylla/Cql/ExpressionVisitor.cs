// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Cassandra;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Cql
{
    public class ExpressionVisitor<TResult> : ExpressionVisitor
    {
        private enum Restriction
        {
            Equality,
            In,
            Range
        }

        private static readonly MethodInfo MapMethod =
            typeof(EntityMapper).GetMethod(nameof(EntityMapper.Map), new[] { typeof(Row), typeof(Type), typeof(IEntityBuilder) })!;

        private readonly Func<Type, IEntityBuilder> _getBuilder;
        private readonly List<string> _where = new();
        private readonly List<object> _parameters = new();
        private readonly Dictionary<string, Restriction> _restrictions = new();
        private readonly List<string> _orderByColumns = new();
        private readonly List<string> _orderBy = new();
        private bool _forceFiltering;
        private bool _filteringAllowed;
        private Type? _entityType;
        private LambdaExpression? _selector;

        public ExpressionVisitor(Func<Type, IEntityBuilder> getBuilder)
        {
            _getBuilder = getBuilder ?? throw new ArgumentNullException(nameof(getBuilder));
        }

        public object[] Parameters => _parameters.ToArray();

        public int? Limit
        {
            get; private set;
        }

        public Func<Row, TResult>? Projector
        {
            get; private set;
        }

        /// <summary>AsNoTracking was called.</summary>
        public bool NoTracking { get; private set; }

        /// <summary>Whole entities, to be tracked: no Select, no AsNoTracking.</summary>
        public bool ReturnsTrackedEntities => _selector == null && !NoTracking;

        /// <summary>WithOptions / WithConsistency / WithTimeout of the query, if any.</summary>
        public Database.StatementOptions? Options { get; private set; }

        private IEntityBuilder Builder
        {
            get => field ?? throw new InvalidOperationException("Query root must be a DbSet");
            set;
        }

        public string VisitExpression(Expression expression)
        {
            Visit(expression);
            ValidateOrderBy();

            var sb = new StringBuilder($"SELECT {BuildSelectList()} FROM {Builder.GetFullTableName()}");
            AppendWhere(sb);

            if (_orderBy.Count > 0)
                sb.Append($" ORDER BY {string.Join(", ", _orderBy)}");

            // CQL requires a strictly positive LIMIT; Take(0) is short-circuited by the provider.
            if (Limit > 0)
                sb.Append($" LIMIT {Limit}");

            AppendFiltering(sb);

            Projector = BuildProjector();
            return sb.ToString();
        }

        /// <summary>
        /// The table and WHERE clause of a query's Where calls, for an UPDATE
        /// (ExecuteUpdate): no Select, OrderBy or Take; Scylla checks that the
        /// whole primary key is given.
        /// </summary>
        public (string Table, string Where) VisitUpdateTarget(Expression expression)
        {
            Visit(expression);
            if (_selector is not null || _orderBy.Count > 0 || Limit is not null || _filteringAllowed)
                throw new NotSupportedException("ExecuteUpdate takes only Where calls that give the row's primary key.");
            if (_where.Count == 0)
                throw new NotSupportedException("ExecuteUpdate needs a Where that gives the row's primary key.");
            return (Builder.GetFullTableName(), string.Join(" AND ", _where));
        }

        internal IEntityBuilder EntityBuilder => Builder;

        public string VisitCount(Expression expression)
        {
            Visit(expression);

            var sb = new StringBuilder($"SELECT COUNT(*) FROM {Builder.GetFullTableName()}");
            AppendWhere(sb);
            AppendFiltering(sb);
            return sb.ToString();
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type.IsGenericType && node.Type.GetGenericTypeDefinition() == typeof(DbSet<>))
            {
                _entityType = node.Type.GetGenericArguments()[0];
                Builder = _getBuilder(_entityType);
            }
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.IsGenericMethod
                && node.Method.GetGenericMethodDefinition() == Linq.ScyllaQueryableExtensions.AllowFilteringMethod)
            {
                Visit(node.Arguments[0]);
                _filteringAllowed = true;
                return node;
            }

            if (node.Method.IsGenericMethod
                && node.Method.GetGenericMethodDefinition() == Linq.ScyllaQueryableExtensions.WithOptionsMethod)
            {
                Visit(node.Arguments[0]);
                var options = (Database.StatementOptions)((ConstantExpression)node.Arguments[1]).Value!;
                // Later calls refine earlier ones: WithConsistency(..).WithTimeout(..).
                Options = Options is null ? options : new Database.StatementOptions(
                    options.Consistency ?? Options.Consistency,
                    options.SerialConsistency ?? Options.SerialConsistency,
                    options.Timeout ?? Options.Timeout);
                return node;
            }

            if (node.Method.IsGenericMethod
                && node.Method.GetGenericMethodDefinition() == Linq.ScyllaQueryableExtensions.AsNoTrackingMethod)
            {
                Visit(node.Arguments[0]);
                NoTracking = true;
                return node;
            }

            if (node.Method.DeclaringType != typeof(Queryable))
                throw new NotSupportedException($"Method {node.Method.Name} cannot be translated to CQL");

            Visit(node.Arguments[0]);

            switch (node.Method.Name)
            {
                case "Where":
                    HandleWhere(node);
                    break;
                case "Select":
                    HandleSelect(node);
                    break;
                case "OrderBy":
                    HandleOrderBy(node, ascending: true, reset: true);
                    break;
                case "OrderByDescending":
                    HandleOrderBy(node, ascending: false, reset: true);
                    break;
                case "ThenBy":
                    HandleOrderBy(node, ascending: true, reset: false);
                    break;
                case "ThenByDescending":
                    HandleOrderBy(node, ascending: false, reset: false);
                    break;
                case "Take":
                    HandleTake(node);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Queryable.{node.Method.Name} cannot be translated to CQL; call AsEnumerable() before it to evaluate on the client");
            }

            return node;
        }

        #region Query Operators

        private void HandleWhere(MethodCallExpression node)
        {
            EnsureComposable("Where");
            var lambda = GetLambda(node);
            TranslatePredicate(lambda.Body, lambda.Parameters[0]);
        }

        private void HandleSelect(MethodCallExpression node)
        {
            var lambda = GetLambda(node);
            _selector = _selector == null
                ? lambda
                : Expression.Lambda(Expression.Invoke(lambda, _selector.Body), _selector.Parameters);
        }

        private void HandleOrderBy(MethodCallExpression node, bool ascending, bool reset)
        {
            EnsureComposable(node.Method.Name);
            var lambda = GetLambda(node);

            if (!TryGetColumn(lambda.Body, lambda.Parameters[0], out var column, out _))
                throw new NotSupportedException("ORDER BY supports only a mapped column");

            if (reset)
            {
                _orderByColumns.Clear();
                _orderBy.Clear();
            }

            _orderByColumns.Add(column);
            _orderBy.Add($"{column} {(ascending ? "ASC" : "DESC")}");
        }

        private void HandleTake(MethodCallExpression node)
        {
            if (node.Arguments[1].Type != typeof(int))
                throw new NotSupportedException("Take(Range) cannot be translated to CQL");

            var count = Math.Max(0, (int)Evaluate(node.Arguments[1])!);
            Limit = Limit.HasValue ? Math.Min(Limit.Value, count) : count;
        }

        private void EnsureComposable(string method)
        {
            if (_selector != null)
                throw new NotSupportedException($"{method} after Select cannot be translated to CQL");

            // CQL always applies WHERE and ORDER BY before LIMIT.
            if (Limit.HasValue)
                throw new NotSupportedException($"{method} after Take cannot be translated to CQL");
        }

        private static LambdaExpression GetLambda(MethodCallExpression node)
        {
            if (node.Arguments.Count != 2 ||
                StripQuotes(node.Arguments[1]) is not LambdaExpression { Parameters.Count: 1 } lambda)
                throw new NotSupportedException($"This overload of Queryable.{node.Method.Name} cannot be translated to CQL");

            return lambda;
        }

        #endregion

        #region Predicates

        private void TranslatePredicate(Expression expr, ParameterExpression parameter)
        {
            switch (expr)
            {
                case BinaryExpression { NodeType: ExpressionType.AndAlso } be:
                    TranslatePredicate(be.Left, parameter);
                    TranslatePredicate(be.Right, parameter);
                    return;

                case BinaryExpression { NodeType: ExpressionType.OrElse }:
                    throw new NotSupportedException("CQL does not support OR; use a collection Contains() to produce IN");

                case BinaryExpression { NodeType: ExpressionType.NotEqual }:
                    throw new NotSupportedException("CQL does not support != in WHERE");

                case BinaryExpression be when ToCqlOperator(be.NodeType) != null:
                    TranslateComparison(be, parameter);
                    return;

                case UnaryExpression { NodeType: ExpressionType.Not } ue
                    when TryGetColumn(ue.Operand, parameter, out var negated, out var negatedType) && IsBoolean(negatedType):
                    AddClause(negated, "=", false, Restriction.Equality);
                    return;

                case UnaryExpression { NodeType: ExpressionType.Not }:
                    throw new NotSupportedException("CQL does not support NOT");

                case MethodCallExpression { Method.Name: "Contains" } mc:
                    TranslateContains(mc, parameter);
                    return;

                case var _ when TryGetColumn(expr, parameter, out var flag, out var flagType) && IsBoolean(flagType):
                    AddClause(flag, "=", true, Restriction.Equality);
                    return;

                default:
                    throw new NotSupportedException($"Unsupported predicate: {expr}");
            }
        }

        private void TranslateComparison(BinaryExpression be, ParameterExpression parameter)
        {
            var nodeType = be.NodeType;
            var valueExpr = be.Right;

            if (!TryGetColumn(be.Left, parameter, out var column, out _))
            {
                if (!TryGetColumn(be.Right, parameter, out column, out _))
                    throw new NotSupportedException($"Comparison must be between a mapped column and a value: {be}");

                valueExpr = be.Left;
                nodeType = Flip(nodeType);
            }

            if (DependsOnParameter(valueExpr))
                throw new NotSupportedException($"Comparison between two columns cannot be translated to CQL: {be}");

            var value = Evaluate(valueExpr)
                ?? throw new NotSupportedException("CQL cannot filter rows by null");

            AddClause(column, ToCqlOperator(nodeType)!, value,
                nodeType == ExpressionType.Equal ? Restriction.Equality : Restriction.Range);
        }

        private void TranslateContains(MethodCallExpression mc, ParameterExpression parameter)
        {
            if (mc.Method.DeclaringType == typeof(string))
                throw new NotSupportedException("CQL has no substring search on regular columns");

            Expression collection;
            Expression item;
            if (mc.Object != null && mc.Arguments.Count == 1)
            {
                collection = mc.Object;
                item = mc.Arguments[0];
            }
            else if (mc.Object == null && mc.Arguments.Count == 2)
            {
                collection = UnwrapSpanConversion(mc.Arguments[0]);
                item = mc.Arguments[1];
            }
            else
            {
                throw new NotSupportedException($"This overload of Contains cannot be translated to CQL: {mc}");
            }

            if (TryGetColumn(item, parameter, out var column, out var itemType) && !DependsOnParameter(collection))
            {
                if (Evaluate(collection) is not IEnumerable values)
                    throw new NotSupportedException("Contains() requires a non-null collection of values");

                // The driver serializes IN ? as list<T>, so the bound value must be strongly typed.
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
                foreach (var value in values)
                    list.Add(value);

                AddClause(column, "IN", list, Restriction.In);
                return;
            }

            if (TryGetColumn(collection, parameter, out column, out _) && !DependsOnParameter(item))
            {
                var value = Evaluate(item)
                    ?? throw new NotSupportedException("CQL cannot filter rows by null");

                _where.Add($"{column} CONTAINS ?");
                _parameters.Add(value);
                _forceFiltering = true;
                return;
            }

            throw new NotSupportedException($"Contains() cannot be translated to CQL: {mc}");
        }

        private void AddClause(string column, string op, object value, Restriction restriction)
        {
            _where.Add($"{column} {op} ?");
            _parameters.Add(value is IList list && op == "IN" ? ConvertList(column, list) : Provider(column, value)!);

            if (!_restrictions.TryGetValue(column, out var existing) || existing == Restriction.Range)
                _restrictions[column] = restriction;
        }

        // A value compared with a column, as the column holds it (its property's converter).
        private object? Provider(string column, object? value)
        {
            var property = PropertyOf(column);
            return property is null ? value : Builder.ToProvider(property, value);
        }

        private string? PropertyOf(string column)
            => Builder.GetMappedProperties().FirstOrDefault(p => Builder.GetColumn(p) == column);

        private IList ConvertList(string column, IList values)
        {
            var property = PropertyOf(column);
            if (property is null || Builder.GetConverter(property) is not { } converter)
                return values;
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(converter.ProviderType))!;
            foreach (var value in values)
                list.Add(converter.ToProvider(value));
            return list;
        }

        private static string? ToCqlOperator(ExpressionType type) =>
            type switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                _ => null
            };

        private static ExpressionType Flip(ExpressionType type) =>
            type switch
            {
                ExpressionType.GreaterThan => ExpressionType.LessThan,
                ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
                ExpressionType.LessThan => ExpressionType.GreaterThan,
                ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
                _ => type
            };

        #endregion

        #region Clause Building

        private void AppendWhere(StringBuilder sb)
        {
            if (_where.Count > 0)
                sb.Append($" WHERE {string.Join(" AND ", _where)}");
        }

        private void AppendFiltering(StringBuilder sb)
        {
            if (!RequiresFiltering())
                return;

            // Scylla itself refuses such queries; adding ALLOW FILTERING silently
            // would turn an innocent-looking Where into a full scan.
            if (!_filteringAllowed)
                throw new NotSupportedException(
                    "This query is not restricted by the full partition key (or filters on a non-key column), " +
                    "so Scylla would have to scan. Restrict the partition key, or call .AllowFiltering() " +
                    "if the scan is intended.");

            sb.Append(" ALLOW FILTERING");
        }

        private bool RequiresFiltering()
        {
            if (_forceFiltering)
                return true;

            if (_restrictions.Count == 0)
                return false;

            var partition = GetColumns(Builder.GetPartitionKey());
            var clustering = GetColumns(Builder.GetClusteringKey());

            if (_restrictions.Keys.Any(c => !partition.Contains(c) && !clustering.Contains(c)))
                return true;

            if (!IsPartitionRestricted(partition))
                return true;

            // Clustering restrictions must form a prefix, with at most the last one being a range.
            var prefixEnded = false;
            foreach (var column in clustering)
            {
                if (!_restrictions.TryGetValue(column, out var restriction))
                {
                    prefixEnded = true;
                    continue;
                }

                if (prefixEnded)
                    return true;

                if (restriction == Restriction.Range)
                    prefixEnded = true;
            }

            return false;
        }

        private void ValidateOrderBy()
        {
            if (_orderByColumns.Count == 0)
                return;

            if (!IsPartitionRestricted(GetColumns(Builder.GetPartitionKey())))
                throw new NotSupportedException(
                    "ORDER BY requires every partition key column to be restricted by == or Contains()");

            var clustering = GetColumns(Builder.GetClusteringKey());
            for (int i = 0; i < _orderByColumns.Count; i++)
            {
                if (i >= clustering.Count || clustering[i] != _orderByColumns[i])
                    throw new NotSupportedException(
                        $"ORDER BY is only supported on clustering columns in declared order ({string.Join(", ", clustering)})");
            }
        }

        private bool IsPartitionRestricted(List<string> partition) =>
            partition.Count > 0 && partition.All(c =>
                _restrictions.TryGetValue(c, out var r) && r != Restriction.Range);

        private List<string> GetColumns(IEnumerable<string> properties) =>
            properties.Select(Builder.GetColumn).ToList();

        private string BuildSelectList()
        {
            if (_selector == null)
                return "*";

            var mapped = Builder.GetMappedProperties().ToHashSet();
            var collector = new MemberCollector(_selector.Parameters[0], mapped);
            collector.Visit(_selector.Body);

            if (collector.UsesWholeEntity || collector.Members.Count == 0)
                return "*";

            return string.Join(", ", collector.Members.Select(Builder.GetColumn));
        }

        private Func<Row, TResult> BuildProjector()
        {
            var row = Expression.Parameter(typeof(Row), "row");
            var entity = Expression.Variable(_entityType!, "entity");
            var map = Expression.Convert(
                Expression.Call(MapMethod, row,
                    Expression.Constant(_entityType, typeof(Type)),
                    Expression.Constant(Builder, typeof(IEntityBuilder))),
                _entityType!);

            Expression result = _selector == null
                ? entity
                : new ParameterReplacer(_selector.Parameters[0], entity).Visit(_selector.Body);

            if (result.Type != typeof(TResult))
                result = Expression.Convert(result, typeof(TResult));

            var body = Expression.Block(new[] { entity }, Expression.Assign(entity, map), result);
            return Expression.Lambda<Func<Row, TResult>>(body, row).Compile();
        }

        #endregion

        #region Helpers

        private bool TryGetColumn(Expression expr, ParameterExpression parameter, out string column, out Type type)
        {
            if (StripConvert(expr) is MemberExpression { Member: PropertyInfo } me && me.Expression == parameter)
            {
                column = Builder.GetColumn(me.Member.Name);
                type = me.Type;
                return true;
            }

            column = string.Empty;
            type = typeof(object);
            return false;
        }

        private static bool IsBoolean(Type type) =>
            type == typeof(bool) || type == typeof(bool?);

        private static object? Evaluate(Expression expr) =>
            expr is ConstantExpression ce
                ? ce.Value
                : Expression.Lambda<Func<object?>>(Expression.Convert(expr, typeof(object))).Compile()();

        private static bool DependsOnParameter(Expression expr)
        {
            var finder = new ParameterFinder();
            finder.Visit(expr);
            return finder.Found;
        }

        // C# 14 binds array.Contains(x) to MemoryExtensions.Contains(ReadOnlySpan<T>, T).
        private static Expression UnwrapSpanConversion(Expression expr) =>
            expr switch
            {
                MethodCallExpression { Method.Name: "op_Implicit", Arguments.Count: 1 } mc => mc.Arguments[0],
                UnaryExpression { NodeType: ExpressionType.Convert, Method.Name: "op_Implicit" } ue => ue.Operand,
                _ => expr
            };

        private static Expression StripConvert(Expression e)
        {
            while (e is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } ue)
                e = ue.Operand;
            return e;
        }

        private static Expression StripQuotes(Expression e) =>
            e.NodeType == ExpressionType.Quote ? ((UnaryExpression)e).Operand : e;

        private sealed class ParameterFinder : System.Linq.Expressions.ExpressionVisitor
        {
            private readonly HashSet<ParameterExpression> _declared = new();

            public bool Found
            {
                get; private set;
            }

            protected override Expression VisitLambda<T>(Expression<T> node)
            {
                _declared.UnionWith(node.Parameters);
                return base.VisitLambda(node);
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (!_declared.Contains(node))
                    Found = true;
                return node;
            }
        }

        private sealed class MemberCollector : System.Linq.Expressions.ExpressionVisitor
        {
            private readonly ParameterExpression _parameter;
            private readonly HashSet<string> _mapped;

            public MemberCollector(ParameterExpression parameter, HashSet<string> mapped)
            {
                _parameter = parameter;
                _mapped = mapped;
            }

            public List<string> Members { get; } = new();

            public bool UsesWholeEntity
            {
                get; private set;
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression == _parameter)
                {
                    // Unmapped members (computed or ignored) are evaluated on the client from the full row.
                    if (_mapped.Contains(node.Member.Name))
                    {
                        if (!Members.Contains(node.Member.Name))
                            Members.Add(node.Member.Name);
                    }
                    else
                    {
                        UsesWholeEntity = true;
                    }
                    return node;
                }
                return base.VisitMember(node);
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _parameter)
                    UsesWholeEntity = true;
                return node;
            }
        }

        private sealed class ParameterReplacer : System.Linq.Expressions.ExpressionVisitor
        {
            private readonly ParameterExpression _from;
            private readonly Expression _to;

            public ParameterReplacer(ParameterExpression from, Expression to)
            {
                _from = from;
                _to = to;
            }

            protected override Expression VisitParameter(ParameterExpression node) =>
                node == _from ? _to : node;
        }

        #endregion
    }
}
