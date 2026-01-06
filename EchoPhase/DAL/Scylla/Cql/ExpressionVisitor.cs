using System.Linq.Expressions;
using System.Text;
using Cassandra;

namespace EchoPhase.DAL.Scylla.Cql
{
    public class ExpressionVisitor<TResult> : ExpressionVisitor
    {
        private string? _table;
        private string? _where;
        private string? _orderBy;
        private int? _limit;
        private List<string> _selectColumns = new();
        private string? _aggregateFunction;

        public bool RequiresClientEval { get; private set; } = false;
        public string? ClientAggregate
        {
            get; private set;
        }

        public Func<Row, TResult>? Projector
        {
            get; private set;
        }

        private static readonly Dictionary<string, Action<ExpressionVisitor<TResult>, MethodCallExpression>> MethodHandlers =
            new()
            {
                ["Where"] = (v, n) => v.HandleWhere(n),
                ["Select"] = (v, n) => v.HandleSelect(n),
                ["OrderBy"] = (v, n) => v.HandleOrderBy(n, ascending: true),
                ["OrderByDescending"] = (v, n) => v.HandleOrderBy(n, ascending: false),
                ["Take"] = (v, n) => v.HandleTake(n),
                ["Count"] = (v, n) => v.HandleCount(n),
                ["Any"] = (v, n) => v.HandleAny(n),
                ["First"] = (v, n) => v.HandleFirst(n),
                ["FirstOrDefault"] = (v, n) => v.HandleFirst(n),
                ["Single"] = (v, n) => v.HandleFirst(n),
                ["SingleOrDefault"] = (v, n) => v.HandleFirst(n),

                ["Sum"] = (v, n) => v.HandleClientAggregate("Sum", n),
                ["Average"] = (v, n) => v.HandleClientAggregate("Average", n),
                ["Min"] = (v, n) => v.HandleClientAggregate("Min", n),
                ["Max"] = (v, n) => v.HandleClientAggregate("Max", n),
                ["GroupBy"] = (v, n) => v.HandleClientAggregate("GroupBy", n)
            };

        public string VisitExpression(Expression expression)
        {
            Visit(expression);

            string select;
            if (!string.IsNullOrEmpty(_aggregateFunction))
                select = _aggregateFunction;
            else if (_selectColumns.Count > 0)
                select = string.Join(", ", _selectColumns);
            else
                select = "*";

            var sb = new StringBuilder();
            sb.Append($"SELECT {select} FROM {_table}");

            if (!string.IsNullOrEmpty(_where))
                sb.Append($" WHERE {_where}");

            if (!string.IsNullOrEmpty(_orderBy))
                sb.Append($" ORDER BY {_orderBy}");

            if (_limit.HasValue)
                sb.Append($" LIMIT {_limit}");

            return sb.ToString();
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type.IsGenericType && node.Type.GetGenericTypeDefinition() == typeof(DbSet<>))
            {
                var entityType = node.Type.GetGenericArguments()[0];
                _table = entityType.Name.ToLower() + "s";
            }
            return base.VisitConstant(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (MethodHandlers.TryGetValue(node.Method.Name, out var handler))
                handler(this, node);
            else
                Visit(node.Arguments[0]);

            return node;
        }

        private void HandleSelect(MethodCallExpression node)
        {
            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);

            if (lambda.Body is NewExpression nex)
            {
                if (nex.Members != null && nex.Members.Count == nex.Arguments.Count)
                {
                    _selectColumns = nex.Members.Select(m => m.Name.ToLower()).ToList();

                    Projector = row =>
                    {
                        var args = new object?[nex.Arguments.Count];
                        for (int i = 0; i < nex.Arguments.Count; i++)
                        {
                            var memberName = nex.Members[i].Name.ToLower();
                            args[i] = row[memberName];
                        }
                        return (TResult)Activator.CreateInstance(nex.Type, args)!;
                    };
                }
                else
                {
                    Projector = row => (TResult)Activator.CreateInstance(nex.Type)!;
                }
            }
            else if (lambda.Body is MemberExpression me)
            {
                var columnName = me.Member.Name.ToLower();
                _selectColumns.Add(columnName);

                Projector = row => (TResult)row[columnName]!;
            }
            else
            {
                Projector = row => (TResult)Activator.CreateInstance(typeof(TResult))!;
            }

            Visit(node.Arguments[0]);
        }

        private void HandleWhere(MethodCallExpression node)
        {
            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
            _where = VisitPredicate(lambda.Body);
            Visit(node.Arguments[0]);
        }

        private void HandleOrderBy(MethodCallExpression node, bool ascending)
        {
            var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
            if (lambda.Body is MemberExpression me)
                _orderBy = me.Member.Name.ToLower() + (ascending ? " ASC" : " DESC");
            Visit(node.Arguments[0]);
        }

        private void HandleTake(MethodCallExpression node)
        {
            if (node.Arguments[1] is ConstantExpression ce)
                _limit = (int)ce.Value!;
            Visit(node.Arguments[0]);
        }

        private void HandleCount(MethodCallExpression node)
        {
            _aggregateFunction = "COUNT(*)";
            Visit(node.Arguments[0]);
        }

        private void HandleAny(MethodCallExpression node)
        {
            _aggregateFunction = "COUNT(*)";
            Visit(node.Arguments[0]);
            Projector = row => (TResult)(object)((long)row["count"] > 0);
        }

        private void HandleFirst(MethodCallExpression node)
        {
            _limit = 1;
            Visit(node.Arguments[0]);
        }

        private void HandleClientAggregate(string type, MethodCallExpression node)
        {
            RequiresClientEval = true;
            ClientAggregate = type;
            Visit(node.Arguments[0]);
        }

        private string VisitPredicate(Expression expr) =>
            expr switch
            {
                BinaryExpression be => $"{VisitPredicate(be.Left)} {ToSqlOperator(be.NodeType)} {VisitPredicate(be.Right)}",
                MemberExpression me => me.Member.Name.ToLower(),
                ConstantExpression ce => FormatConstant(ce.Value),
                UnaryExpression ue when ue.NodeType == ExpressionType.Not =>
                    $"NOT ({VisitPredicate(ue.Operand)})",
                _ => throw new NotSupportedException($"Unsupported expression: {expr.NodeType}")
            };

        private string ToSqlOperator(ExpressionType type) =>
            type switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "!=",
                ExpressionType.AndAlso => "AND",
                ExpressionType.OrElse => "OR",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                _ => throw new NotSupportedException($"Unsupported operator: {type}")
            };

        private string FormatConstant(object? value) =>
            value switch
            {
                null => "null",
                string s => $"'{s.Replace("'", "''")}'",
                Guid g => g.ToString(),
                int i => i.ToString(),
                _ => value!.ToString()!
            };

        private static Expression StripQuotes(Expression e) =>
            e.NodeType == ExpressionType.Quote ? ((UnaryExpression)e).Operand : e;
    }
}
