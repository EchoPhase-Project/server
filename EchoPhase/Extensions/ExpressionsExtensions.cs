using System.Linq.Expressions;

namespace EchoPhase.Extensions
{
    public static class ExpressionsExtensions
    {
        public static Expression StripQuotes(this Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
                e = ((UnaryExpression)e).Operand;
            return e;
        }
    }
}
