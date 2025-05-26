using System.Collections;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

using EchoPhase.Extensions;
using EchoPhase.Attributes;
using EchoPhase.Helpers.Options;

namespace EchoPhase.Helpers
{
	public class ProjectionHelper
	{
		private ProjectionOptions _options = new();

		public ProjectionHelper() {}

		public ProjectionHelper WithOptions(ProjectionOptions options)
		{
			_options = options;
			return this;
		}

		public ProjectionHelper WithOptions(Action<ProjectionOptions> configure)
		{
			configure(_options);
			return this;
		}

		public object Project<T>(
			T source,
			params Expression<Func<T, object>>[] includeProperties)
		{
			if (source == null) return null!;

			var fields = includeProperties.Select(ExtractMemberPath).ToHashSet();
			CollectExposePaths(fields, typeof(T), "");

			return _options.useExpando
				? (object)ProjectToExpando(source!, fields, 0)
				: (object)ProjectToDictionary(source!, fields, 0);
		}

		private Dictionary<string, object?> ProjectToDictionary(
			object source,
			ISet<string> includePaths,
			int depth
		)
		{
			var dict = new Dictionary<string, object?>();
			FillDict(dict, source, includePaths, depth, ProjectToDictionary);

			return dict;
		}

		private ExpandoObject ProjectToExpando(
			object source,
			ISet<string> includePaths,
			int depth
		)
		{
			var expando = new ExpandoObject();
			FillDict(expando, source, includePaths, depth, ProjectToExpando);

			return expando;
		}

		private void CollectExposePaths(in ISet<string> fields, Type type, string basePath)
		{
			foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				var path = string.IsNullOrEmpty(basePath) ? prop.Name : $"{basePath}.{prop.Name}";

				if (Attribute.IsDefined(prop, typeof(ExposeAttribute)))
					fields.Add(path);

				if (!IsSimple(prop.PropertyType))
					CollectExposePaths(fields, prop.PropertyType, path);
			}
		}

		private void FillDict(in IDictionary<string, object?> dict,
			object source,
			ISet<string> includePaths,
			int depth,
			Func<object, ISet<string>, int, object> recursion
		)
		{
			var type = source.GetType();
			var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (var prop in props)
			{
				string path = prop.Name;
				if (!includePaths.Any(p => p.StartsWith(path))) continue;

				var value = prop.GetValue(source);
				if (value == null)
				{
					dict[path] = null;
					continue;
				}

				if (IsSimple(value.GetType()))
				{
					dict[path] = value;
				}
				else if (value is IEnumerable enumerable and not string)
				{
					var list = new List<object?>();
					foreach (var item in enumerable)
					{
						list.Add(IsSimple(item?.GetType() ?? typeof(object))
							? item
							: recursion(item!, SubPaths(includePaths, path), depth + 1));
					}

					dict[path] = list;
				}
				else
				{
					if (_options.maxDepth != null && depth >= _options.maxDepth) continue;
					dict[path] = recursion(value, SubPaths(includePaths, path), depth + 1);
				}
			}
		}

		private string ExtractMemberPath<T>(Expression<Func<T, object>> expr)
		{
			if (expr.Body is MemberExpression member)
				return member.Member.Name;

			if (expr.Body is UnaryExpression unary && unary.Operand is MemberExpression inner)
				return inner.ToString().Split('.').Skip(1).Aggregate((a, b) => $"{a}.{b}");

			if (expr.Body is MethodCallExpression methodCall && methodCall.Method.Name == "Select")
			{
				var outerS = ((MemberExpression)((LambdaExpression)methodCall.Arguments[0].StripQuotes()).Body).Member.Name;
				var innerS = ((MemberExpression)((LambdaExpression)methodCall.Arguments[1].StripQuotes()).Body).Member.Name;
				return $"{outerS}.{innerS}";
			}

			throw new InvalidOperationException("Unsupported expression: " + expr);
		}

		private ISet<string> SubPaths(ISet<string> paths, string parent)
		{
			var prefix = parent + ".";
			return paths
				.Where(p => p.StartsWith(prefix))
				.Select(p => p[prefix.Length..])
				.ToHashSet();
		}

		protected static bool IsSimple(Type type)
		{
			return
				type.IsPrimitive ||
				type.IsEnum ||
				type == typeof(string) ||
				type == typeof(decimal) ||
				type == typeof(DateTime) ||
				type == typeof(Guid) ||
				type == typeof(DateTimeOffset) ||
				type == typeof(TimeSpan);
		}
	}
}
