using System.Collections;
using System.Collections.Concurrent;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using EchoPhase.Projection.Attributes;
using EchoPhase.Projection.Options;
using Newtonsoft.Json.Linq;

namespace EchoPhase.Projection
{
    public class Projector
    {
        internal readonly ProjectionOptions _defaultOptions;

        private static readonly ConcurrentDictionary<Type, (PropertyInfo Prop, bool IsExposed)[]> _propCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlySet<string>> _exposePathCache = new();

        public Projector() => _defaultOptions = new ProjectionOptions();
        public Projector(ProjectionOptions defaultOptions) => _defaultOptions = defaultOptions;

        public ProjectionBuilder<T> For<T>(T source) =>
            new(source, _defaultOptions.Clone());

        public CollectionProjectionBuilder<T> ForCollection<T>(IEnumerable<T> source) =>
            new(source, _defaultOptions.Clone());

        internal object? Execute<T>(
            T source,
            ProjectionOptions options,
            IReadOnlyList<Expression<Func<T, object?>>> includeProperties,
            IReadOnlyDictionary<string, CollectionConfig>? collectionConfigs = null)
        {
            if (source == null) return null;

            HashSet<string>? fields;
            if (includeProperties.Count > 0)
            {
                fields = includeProperties.Select(ExtractMemberPath)
                                          .ToHashSet(StringComparer.Ordinal);
            }
            else
            {
                fields = options.IncludeOnlyExpose
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : null;
            }

            if (fields != null && options.IncludeOnlyExpose)
            {
                var cached = _exposePathCache.GetOrAdd(source.GetType(), t =>
                {
                    var set = new HashSet<string>(StringComparer.Ordinal);
                    CollectExposePaths(set, t, "", new HashSet<Type>());
                    return set;
                });
                foreach (var path in cached)
                    fields.Add(path);
            }

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            collectionConfigs ??= new Dictionary<string, CollectionConfig>();

            return options.UseExpando
                ? (object)ProjectToExpando(source!, fields, visited, options, collectionConfigs)
                : ProjectToDictionary(source!, fields, visited, options, collectionConfigs);
        }

        private Dictionary<string, object?> ProjectToDictionary(
            object source, ISet<string>? includePaths,
            HashSet<object> visited, ProjectionOptions options,
            IReadOnlyDictionary<string, CollectionConfig> collectionConfigs)
        {
            var dict = new Dictionary<string, object?>();
            FillDict(dict, source, includePaths, visited, options, collectionConfigs,
                (s, p, v) => ProjectToDictionary(s, p, v, options, collectionConfigs));
            return dict;
        }

        private ExpandoObject ProjectToExpando(
            object source, ISet<string>? includePaths,
            HashSet<object> visited, ProjectionOptions options,
            IReadOnlyDictionary<string, CollectionConfig> collectionConfigs)
        {
            var expando = new ExpandoObject();
            FillDict((IDictionary<string, object?>)expando, source, includePaths, visited, options, collectionConfigs,
                (s, p, v) => ProjectToExpando(s, p, v, options, collectionConfigs));
            return expando;
        }

        private object? ConvertToObject(
            object? source, ISet<string>? includePaths,
            HashSet<object> visited, ProjectionOptions options,
            IReadOnlyDictionary<string, CollectionConfig> collectionConfigs,
            Func<object, ISet<string>?, HashSet<object>, object?> recursion,
            CollectionConfig? collectionConfig = null)
        {
            if (source == null) return null;

            if (source is JsonElement jsonElem)
            {
                switch (jsonElem.ValueKind)
                {
                    case JsonValueKind.Object:
                        var jdict = new Dictionary<string, object?>();
                        FillDict(jdict, jsonElem, includePaths, visited, options, collectionConfigs, recursion);
                        return jdict;
                    case JsonValueKind.Array:
                        var jlist = new List<object?>();
                        foreach (var item in jsonElem.EnumerateArray())
                            jlist.Add(ConvertToObject(item, includePaths, visited, options, collectionConfigs, recursion));
                        return jlist;
                    default:
                        return GetJsonSimpleValue(jsonElem);
                }
            }

            if (source is JToken jtoken)
            {
                switch (jtoken.Type)
                {
                    case JTokenType.Object:
                        var jtdict = new Dictionary<string, object?>();
                        FillDict(jtdict, jtoken, includePaths, visited, options, collectionConfigs, recursion);
                        return jtdict;
                    case JTokenType.Array:
                        var jtlist = new List<object?>();
                        foreach (var item in (JArray)jtoken)
                            jtlist.Add(ConvertToObject(item, includePaths, visited, options, collectionConfigs, recursion));
                        return jtlist;
                    case JTokenType.Null:
                        return null;
                    default:
                        return jtoken is JValue jval ? jval.Value : jtoken.ToString();
                }
            }

            if (source is IDictionary rawDict && source is not string)
            {
                if (!visited.Add(source)) return null;
                var dict = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in rawDict)
                {
                    var key = entry.Key?.ToString() ?? "";
                    if (includePaths != null && !includePaths.Contains(key)) continue;
                    dict[key] = entry.Value is null
                        ? null
                        : ConvertToObject(entry.Value, includePaths, visited, options, collectionConfigs, recursion);
                }
                visited.Remove(source);
                return dict;
            }

            if (source is IEnumerable enumerable && source is not string)
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    if (item == null) { list.Add(null); continue; }
                    list.Add(collectionConfig != null
                        ? collectionConfig.Apply(this, item).Build()
                        : ConvertToObject(item, includePaths, visited, options, collectionConfigs, recursion));
                }
                return list;
            }

            if (!IsSimple(source.GetType()))
            {
                if (!visited.Add(source)) return null;
                var dict = new Dictionary<string, object?>();
                FillDict(dict, source, includePaths, visited, options, collectionConfigs, recursion);
                visited.Remove(source);
                return dict;
            }

            return source;
        }

        private void FillDict(
            IDictionary<string, object?> dict,
            object source, ISet<string>? includePaths,
            HashSet<object> visited, ProjectionOptions options,
            IReadOnlyDictionary<string, CollectionConfig> collectionConfigs,
            Func<object, ISet<string>?, HashSet<object>, object?> recursion)
        {
            bool ShouldInclude(string path)
            {
                if (includePaths == null) return true;
                if (includePaths.Contains(path)) return true;
                if (!options.IncludeSubPaths) return false;
                var needle = path + ".";
                foreach (var p in includePaths)
                    if (p.StartsWith(needle, StringComparison.Ordinal)) return true;
                return false;
            }

            if (source is JsonElement jsonElem)
            {
                foreach (var prop in jsonElem.EnumerateObject())
                {
                    if (!ShouldInclude(prop.Name)) continue;
                    dict[prop.Name] = ConvertToObject(prop.Value, SubPaths(includePaths, prop.Name),
                        visited, options, collectionConfigs, recursion);
                }
                return;
            }

            if (source is JObject jobject)
            {
                foreach (var prop in jobject.Properties())
                {
                    if (!ShouldInclude(prop.Name)) continue;
                    dict[prop.Name] = ConvertToObject(prop.Value, SubPaths(includePaths, prop.Name),
                        visited, options, collectionConfigs, recursion);
                }
                return;
            }

            foreach (var (prop, _) in GetCachedProperties(source.GetType()))
            {
                if (!ShouldInclude(prop.Name)) continue;

                var val = prop.GetValue(source);
                if (val is null) { dict[prop.Name] = null; continue; }

                collectionConfigs.TryGetValue(prop.Name, out var colConfig);
                dict[prop.Name] = ConvertToObject(
                    val, SubPaths(includePaths, prop.Name),
                    visited, options, collectionConfigs, recursion,
                    collectionConfig: colConfig);
            }
        }

        private void CollectExposePaths(ISet<string> fields, Type type, string basePath, HashSet<Type> visitedTypes)
        {
            if (!visitedTypes.Add(type)) return;

            foreach (var (prop, isExposed) in GetCachedProperties(type))
            {
                var path = basePath.Length == 0
                    ? prop.Name
                    : string.Concat(basePath, ".", prop.Name);

                if (isExposed) fields.Add(path);
                if (!isExposed) continue;

                var propType = prop.PropertyType;
                var elementType = GetCollectionElementType(propType);
                if (elementType != null) propType = elementType;

                if (!IsSimple(propType))
                    CollectExposePaths(fields, propType, path, visitedTypes);
            }

            visitedTypes.Remove(type);
        }

        private static (PropertyInfo Prop, bool IsExposed)[] GetCachedProperties(Type type) =>
            _propCache.GetOrAdd(type, t =>
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Where(p => p.GetIndexParameters().Length == 0)
                 .Select(p => (p, Attribute.IsDefined(p, typeof(ExposeAttribute))))
                 .ToArray());

        private static Type? GetCollectionElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                if (def == typeof(IEnumerable<>) || def == typeof(ICollection<>) ||
                    def == typeof(IList<>) || def == typeof(List<>))
                    return type.GetGenericArguments()[0];

                var iface = type.GetInterfaces().FirstOrDefault(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (iface != null) return iface.GetGenericArguments()[0];
            }
            return null;
        }

        internal static string ExtractMemberPath<T>(Expression<Func<T, object?>> expr)
        {
            Expression body = expr.Body;
            while (body.NodeType == ExpressionType.Convert || body.NodeType == ExpressionType.ConvertChecked)
                body = ((UnaryExpression)body).Operand;

            if (body is MemberExpression member)
            {
                var sb = new StringBuilder();
                BuildPath(member, sb);
                return sb.ToString();
            }

            throw new InvalidOperationException("Unsupported expression: " + expr);
        }

        private static void BuildPath(MemberExpression member, StringBuilder sb)
        {
            if (member.Expression is MemberExpression parent)
            {
                BuildPath(parent, sb);
                sb.Append('.');
            }
            sb.Append(member.Member.Name);
        }

        private static ISet<string>? SubPaths(ISet<string>? paths, string parent)
        {
            if (paths == null) return null;

            var prefix = parent + ".";
            HashSet<string>? result = null;
            foreach (var p in paths)
            {
                if (!p.StartsWith(prefix, StringComparison.Ordinal)) continue;
                result ??= new HashSet<string>(StringComparer.Ordinal);
                result.Add(p[prefix.Length..]);
            }

            return result ?? new HashSet<string>(StringComparer.Ordinal);
        }

        private static bool IsSimple(Type type) =>
            type.IsPrimitive || type.IsEnum ||
            type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(Guid) ||
            type == typeof(DateTimeOffset) || type == typeof(TimeSpan);

        private static object? GetJsonSimpleValue(JsonElement elem) => elem.ValueKind switch
        {
            JsonValueKind.String => elem.GetString(),
            JsonValueKind.Number => elem.TryGetInt64(out long l) ? l
                                  : elem.TryGetDouble(out double d) ? d
                                  : (object?)elem.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => elem.GetRawText()
        };
    }
}
