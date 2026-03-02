using System.Collections;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using EchoPhase.Projection.Attributes;
using EchoPhase.Projection.Options;
using Newtonsoft.Json.Linq;

namespace EchoPhase.Projection
{
    /// <summary>
    /// Provides functionality to project an object into a simplified structure such as a dictionary or ExpandoObject.
    /// Supports inclusion of specific fields, nested paths, and attributes-based filtering.
    /// </summary>
    public class Projector
    {
        private ProjectionOptions _options = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectionHelper"/> class with default options.
        /// </summary>
        public Projector()
        {
        }

        /// <summary>
        /// Sets the projection options to the specified instance.
        /// </summary>
        /// <param name="options">An instance of <see cref="ProjectionOptions"/> to use for projection.</param>
        /// <returns>The current <see cref="ProjectionHelper"/> instance.</returns>
        public Projector WithOptions(ProjectionOptions options)
        {
            _options = options;
            return this;
        }

        /// <summary>
        /// Configures the current projection options using the provided delegate.
        /// </summary>
        /// <param name="configure">An action that modifies the <see cref="ProjectionOptions"/> instance.</param>
        /// <returns>The current <see cref="ProjectionHelper"/> instance.</returns>
        public Projector WithOptions(Action<ProjectionOptions> configure)
        {
            configure(_options);
            return this;
        }

        /// <summary>
        /// Projects the specified source object into a simplified representation,
        /// including only the specified properties or those marked with <see cref="ExposeAttribute"/>.
        /// </summary>
        /// <typeparam name="T">The type of the source object.</typeparam>
        /// <param name="source">The source object to project.</param>
        /// <param name="includeProperties">
        /// An optional list of expressions specifying which properties to include.
        /// If empty, all properties are included unless <see cref="ProjectionOptions.IncludeOnlyExpose"/> is set.
        /// </param>
        /// <returns>
        /// A simplified representation of the object, either as <see cref="ExpandoObject"/>
        /// or <see cref="Dictionary{String, Object}"/>, depending on <see cref="ProjectionOptions.UseExpando"/>.
        /// </returns>
        public object Project<T>(
            T source,
            params Expression<Func<T, object?>>[] includeProperties)
        {
            if (source == null) return null!;

            var fields = includeProperties.Select(ExtractMemberPath).ToHashSet();

            if (fields is { Count: 0 })
                fields = _options.IncludeOnlyExpose ? new HashSet<string>() : null;

            if (fields != null && _options.IncludeOnlyExpose)
                CollectExposePaths(fields, typeof(T), "");

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

            return _options.UseExpando
                ? (object)ProjectToExpando(source!, fields, 0, visited)
                : (object)ProjectToDictionary(source!, fields, 0, visited);
        }

        private Dictionary<string, object?> ProjectToDictionary(
            object source,
            ISet<string>? includePaths,
            int depth,
            HashSet<object> visited)
        {
            var dict = new Dictionary<string, object?>();
            FillDict(dict, source, includePaths, depth, visited,
                (s, p, d, v) => ProjectToDictionary(s, p, d, v));
            return dict;
        }

        private ExpandoObject ProjectToExpando(
            object source,
            ISet<string>? includePaths,
            int depth,
            HashSet<object> visited)
        {
            var expando = new ExpandoObject();
            FillDict((IDictionary<string, object?>)expando, source, includePaths, depth, visited,
                (s, p, d, v) => ProjectToExpando(s, p, d, v));
            return expando;
        }

        private object? ConvertToObject(
            object source,
            ISet<string>? includePaths,
            int depth,
            HashSet<object> visited,
            Func<object, ISet<string>?, int, HashSet<object>, object?> recursion)
        {
            if (source == null)
                return null;

            if (source is JsonElement jsonElem)
            {
                switch (jsonElem.ValueKind)
                {
                    case JsonValueKind.Object:
                        var dict = new Dictionary<string, object?>();
                        FillDict(dict, jsonElem, includePaths, depth, visited, recursion);
                        return dict;

                    case JsonValueKind.Array:
                        var list = new List<object?>();
                        foreach (var item in jsonElem.EnumerateArray())
                            list.Add(ConvertToObject(item, includePaths, depth + 1, visited, recursion));
                        return list;

                    default:
                        return GetJsonSimpleValue(jsonElem);
                }
            }

            if (source is JToken jtoken)
            {
                switch (jtoken.Type)
                {
                    case JTokenType.Object:
                        var dict = new Dictionary<string, object?>();
                        FillDict(dict, jtoken, includePaths, depth, visited, recursion);
                        return dict;

                    case JTokenType.Array:
                        var list = new List<object?>();
                        foreach (var item in (JArray)jtoken)
                            list.Add(ConvertToObject(item, includePaths, depth + 1, visited, recursion));
                        return list;

                    case JTokenType.Null:
                        return null;

                    default:
                        if (jtoken is JValue jval)
                            return jval.Value;
                        else
                            return jtoken.ToString();
                }
            }

            if (source is IDictionary rawDict && source is not string)
            {
                if (!IsSimple(source.GetType()))
                {
                    if (!visited.Add(source))
                        return null;
                }

                var dict = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in rawDict)
                {
                    string key = entry.Key?.ToString() ?? "";
                    if (includePaths != null && !includePaths.Contains(key))
                        continue;
                        dict[key] = entry.Value is null
                            ? null
                            : ConvertToObject(entry.Value, includePaths, depth + 1, visited, recursion);
                }
                visited.Remove(source);
                return dict;
            }

            if (source is IEnumerable enumerable && source is not string)
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                    list.Add(ConvertToObject(item, includePaths, depth + 1, visited, recursion));
                return list;
            }

            if (!IsSimple(source.GetType()))
            {
                if (!visited.Add(source))
                    return null; // циклическая ссылка — обрываем

                var dict = new Dictionary<string, object?>();
                FillDict(dict, source, includePaths, depth, visited, recursion);
                visited.Remove(source);
                return dict;
            }

            return source;
        }

        private void FillDict(
            IDictionary<string, object?> dict,
            object source,
            ISet<string>? includePaths,
            int depth,
            HashSet<object> visited,
            Func<object, ISet<string>?, int, HashSet<object>, object?> recursion)
        {
            bool ShouldInclude(string path) =>
                includePaths == null ||
                includePaths.Contains(path) ||
                (_options.IncludeSubPaths && includePaths.Any(p => p.StartsWith(path + ".")));

            if (source is JsonElement jsonElem)
            {
                foreach (var prop in jsonElem.EnumerateObject())
                {
                    if (!ShouldInclude(prop.Name)) continue;
                    dict[prop.Name] = ConvertToObject(prop.Value, SubPaths(includePaths, prop.Name), depth + 1, visited, recursion);
                }
                return;
            }

            if (source is JObject jobject)
            {
                foreach (var prop in jobject.Properties())
                {
                    if (!ShouldInclude(prop.Name)) continue;
                    dict[prop.Name] = ConvertToObject(prop.Value, SubPaths(includePaths, prop.Name), depth + 1, visited, recursion);
                }
                return;
            }

            var type = source.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                           .Where(p => p.GetIndexParameters().Length == 0);

            foreach (var prop in props)
            {
                if (!ShouldInclude(prop.Name)) continue;
                var val = prop.GetValue(source);
                dict[prop.Name] = ConvertToObject(val!, SubPaths(includePaths, prop.Name), depth + 1, visited, recursion);
            }
        }

        private void CollectExposePaths(in ISet<string> fields, Type type, string basePath)
        {
            CollectExposePaths(fields, type, basePath, new HashSet<Type>());
        }

        private void CollectExposePaths(in ISet<string> fields, Type type, string basePath, HashSet<Type> visitedTypes)
        {
            if (!visitedTypes.Add(type))
                return;

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var path = string.IsNullOrEmpty(basePath) ? prop.Name : $"{basePath}.{prop.Name}";
                var isExposed = Attribute.IsDefined(prop, typeof(ExposeAttribute));

                if (isExposed)
                    fields.Add(path);

                var propType = prop.PropertyType;
                var elementType = GetCollectionElementType(propType);
                if (elementType != null)
                    propType = elementType;

                // Заходим вглубь только если само свойство exposed
                if (isExposed && !IsSimple(propType))
                    CollectExposePaths(fields, propType, path, visitedTypes);
            }

            visitedTypes.Remove(type);
        }

        private static Type? GetCollectionElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                if (def == typeof(IEnumerable<>) ||
                    def == typeof(ICollection<>) ||
                    def == typeof(IList<>) ||
                    def == typeof(List<>))
                    return type.GetGenericArguments()[0];

                // IEnumerable<T> через интерфейс
                var enumerable = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                                         i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (enumerable != null)
                    return enumerable.GetGenericArguments()[0];
            }

            return null;
        }

        private string ExtractMemberPath<T>(Expression<Func<T, object?>> expr)
        {
            Expression body = expr.Body;

            while (body.NodeType == ExpressionType.Convert || body.NodeType == ExpressionType.ConvertChecked)
            {
                body = ((UnaryExpression)body).Operand;
            }

            if (body is MemberExpression member)
            {
                var names = new Stack<string>();
                while (member != null)
                {
                    names.Push(member.Member.Name);
                    if (member.Expression is MemberExpression parent)
                        member = parent;
                    else
                        break;
                }
                var path = string.Join(".", names);
                return path;
            }

            throw new InvalidOperationException("Unsupported expression: " + expr);
        }

        private ISet<string>? SubPaths(ISet<string>? paths, string parent)
        {
            if (paths == null) return null;

            var prefix = parent + ".";
            var sub = paths
                .Where(p => p.StartsWith(prefix))
                .Select(p => p[prefix.Length..])
                .ToHashSet();

            return sub;
        }

        private static bool IsSimple(Type type)
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

        private static object? GetJsonSimpleValue(JsonElement elem)
        {
            switch (elem.ValueKind)
            {
                case JsonValueKind.String: return elem.GetString();
                case JsonValueKind.Number:
                    if (elem.TryGetInt64(out long l)) return l;
                    if (elem.TryGetDouble(out double d)) return d;
                    return elem.GetRawText();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null: return null;
                default: return elem.GetRawText();
            }
        }
    }
}
