using System.Linq.Expressions;
using System.Reflection;
using EchoPhase.Attributes;

namespace EchoPhase.Extensions
{
    public static class MergeExtensions
    {
        public static void MergeFrom<T>(
            this T target,
            T source,
            params Expression<Func<T, object>>[] overrideFields
        )
        {
            if (target == null || source == null)
                throw new ArgumentNullException();

            var visited = new HashSet<object>();

            var overrideNames = overrideFields
                .Select(expr =>
                {
                    if (expr.Body is UnaryExpression unary && unary.Operand is MemberExpression member)
                        return member.Member.Name;
                    if (expr.Body is MemberExpression memberDirect)
                        return memberDirect.Member.Name;

                    throw new InvalidOperationException("Unsupported expression format");
                })
                .ToHashSet();

            MergeRecursive(target!, source!, visited, overrideNames);
        }

        private static void MergeRecursive(
            object target,
            object source,
            HashSet<object> visited,
            HashSet<string> overrideNames
        )
        {
            if (target == null || source == null || visited.Contains(target))
                return;

            visited.Add(target);

            var type = target.GetType();

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p =>
                    overrideNames.Count > 0
                        ? overrideNames.Contains(p.Name)
                        : Attribute.IsDefined(p, typeof(MergeIfNullAttribute)) || Attribute.IsDefined(p, typeof(AlwaysMergeAttribute))
                );

            foreach (var prop in props)
            {
                var targetValue = prop.GetValue(target);
                var sourceValue = prop.GetValue(source);

                bool alwaysMerge = Attribute.IsDefined(prop, typeof(AlwaysMergeAttribute));

                if (alwaysMerge || (targetValue == null && sourceValue != null))
                {
                    prop.SetValue(target, sourceValue);
                }
                else if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string) && sourceValue != null && targetValue != null)
                {
                    MergeRecursive(targetValue, sourceValue, visited, new HashSet<string>());
                }
            }
        }
    }
}
