// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Cql
{
    /// <summary>
    /// An IF clause from a lambda: <c>r => r.Status == "queued" &amp;&amp; r.Steps &lt; 10</c>.
    /// Comparisons of a property with a value (constants and captured
    /// variables), joined by &amp;&amp;; a bool property alone means == true.
    /// </summary>
    public static class ConditionTranslator
    {
        public static WriteCondition Translate<TEntity>(Expression<Func<TEntity, bool>> condition)
        {
            var checks = new List<ConditionCheck>();
            Visit(condition.Body, condition.Parameters[0], checks);
            return WriteCondition.Where([.. checks]);
        }

        private static void Visit(Expression expression, ParameterExpression parameter, List<ConditionCheck> checks)
        {
            switch (expression)
            {
                case BinaryExpression { NodeType: ExpressionType.AndAlso } and:
                    Visit(and.Left, parameter, checks);
                    Visit(and.Right, parameter, checks);
                    return;
                case BinaryExpression binary when Operator(binary.NodeType) is { } op:
                    if (Property(binary.Left, parameter) is { } left)
                        checks.Add(new ConditionCheck(left, op, Evaluate(binary.Right)));
                    else if (Property(binary.Right, parameter) is { } right)
                        checks.Add(new ConditionCheck(right, Flip(op), Evaluate(binary.Left)));
                    else
                        throw new NotSupportedException($"A condition compares a property with a value: {binary}");
                    return;
                case UnaryExpression { NodeType: ExpressionType.Not } not when Property(not.Operand, parameter) is { } negated:
                    checks.Add(new ConditionCheck(negated, "=", false));
                    return;
                case var e when Property(e, parameter) is { } flag && e.Type == typeof(bool):
                    checks.Add(new ConditionCheck(flag, "=", true));
                    return;
                default:
                    throw new NotSupportedException($"Unsupported condition (CQL IF takes comparisons joined by AND): {expression}");
            }
        }

        private static string? Property(Expression expression, ParameterExpression parameter)
        {
            while (expression is UnaryExpression { NodeType: ExpressionType.Convert } convert)
                expression = convert.Operand;
            return expression is MemberExpression { Expression: var owner } member && owner == parameter
                ? member.Member.Name
                : null;
        }

        private static object? Evaluate(Expression expression)
            => expression is ConstantExpression constant
                ? constant.Value
                : Expression.Lambda(Expression.Convert(expression, typeof(object))).Compile().DynamicInvoke();

        private static string? Operator(ExpressionType type) => type switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "!=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            _ => null,
        };

        private static string Flip(string op) => op switch
        {
            "<" => ">",
            "<=" => ">=",
            ">" => "<",
            ">=" => "<=",
            _ => op,
        };
    }
}
